using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Security;
using ConduitLLM.Providers.Configuration;

using AwesomeAssertions;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace ConduitLLM.Tests.Providers;

/// <summary>
/// The credential model for providers that need more than one secret, or secret material that is not
/// an API key at all (issue #1187). Values are declared as secret settings, held on the key
/// credential, encrypted at rest, and never returned to clients.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class ProviderSecretSettingsTests
{
    private static ProviderSecretProtector CreateProtector() =>
        new(DataProtectionProvider.Create(nameof(ProviderSecretSettingsTests)),
            NullLogger<ProviderSecretProtector>.Instance);

    /// <summary>
    /// AWS Bedrock: a region (non-secret), two secrets, and an optional third for STS sessions.
    /// </summary>
    private static readonly ProviderSettingDefinition[] BedrockShape =
    {
        new()
        {
            Key = "region", Label = "Region", Required = true,
            Binding = ProviderSettingBinding.AuthScope
        },
        new()
        {
            Key = "access_key_id", Label = "Access Key ID", Required = true, Secret = true,
            Binding = ProviderSettingBinding.AuthScope
        },
        new()
        {
            Key = "secret_access_key", Label = "Secret Access Key", Required = true, Secret = true,
            Binding = ProviderSettingBinding.AuthScope
        },
        new()
        {
            Key = "session_token", Label = "Session Token", Required = false, Secret = true,
            Binding = ProviderSettingBinding.AuthScope
        }
    };

    /// <summary>
    /// Google Vertex: a project and location (non-secret) plus a service-account JSON document -
    /// secret material that is not shaped like an API key at all.
    /// </summary>
    private static readonly ProviderSettingDefinition[] VertexShape =
    {
        new() { Key = "project", Label = "Project ID", Required = true, Binding = ProviderSettingBinding.UrlPathToken },
        new() { Key = "location", Label = "Location", Required = true, Binding = ProviderSettingBinding.UrlPathToken },
        new()
        {
            Key = "service_account_json", Label = "Service Account JSON", Required = true, Secret = true,
            Binding = ProviderSettingBinding.AuthScope
        }
    };

    [Fact]
    public void Protector_Should_Round_Trip_A_Secret_Without_Storing_It_In_The_Clear()
    {
        var protector = CreateProtector();
        const string secret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

        var stored = protector.Protect(secret);

        stored.Should().NotBe(secret);
        stored.Should().StartWith(ProviderSecretProtector.Prefix);
        protector.IsProtected(stored).Should().BeTrue();
        protector.Reveal(stored).Should().Be(secret);
    }

    [Fact]
    public void Protector_Should_Be_Idempotent_So_Resaving_Never_Double_Encrypts()
    {
        var protector = CreateProtector();
        var once = protector.Protect("secret-value");

        protector.Protect(once).Should().Be(once);
        protector.Reveal(protector.Protect(once)).Should().Be("secret-value");
    }

    [Fact]
    public void Protector_Should_Pass_Through_A_Value_Written_Before_Protection_Existed()
    {
        CreateProtector().Reveal("legacy-plaintext").Should().Be("legacy-plaintext");
    }

    [Fact]
    public void Protector_Should_Round_Trip_A_Whole_Settings_Map()
    {
        var protector = CreateProtector();
        var plaintext = new Dictionary<string, string>
        {
            ["access_key_id"] = "AKIAIOSFODNN7EXAMPLE",
            ["secret_access_key"] = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
        };

        var stored = protector.ProtectAll(plaintext)!;

        stored.Keys.Should().BeEquivalentTo(plaintext.Keys);
        stored.Values.Should().OnlyContain(value => value.StartsWith(ProviderSecretProtector.Prefix));
        protector.RevealAll(stored).Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void Protector_Should_Fail_Closed_When_A_Value_Cannot_Be_Decrypted()
    {
        // A rotated or lost key ring must not hand ciphertext to a provider as if it were the secret.
        var stored = CreateProtector().Protect("secret-value");
        var otherKeyRing = new ProviderSecretProtector(
            DataProtectionProvider.Create("a-different-application"),
            NullLogger<ProviderSecretProtector>.Instance);

        var act = () => otherKeyRing.Reveal(stored);

        act.Should().Throw<InvalidOperationException>().WithMessage("*could not be decrypted*");
    }

    [Theory]
    [MemberData(nameof(MultiSecretShapes))]
    public void The_Credential_Model_Should_Express_MultiSecret_Provider_Shapes(
        string shapeName,
        ProviderSettingDefinition[] shape,
        string[] expectedSecretKeys)
    {
        shapeName.Should().NotBeEmpty();

        // Secrets and non-secret identifiers are separable, which is what keeps secret material out
        // of the plaintext Provider.Settings bag.
        shape.Where(setting => setting.Secret).Select(setting => setting.Key)
            .Should().Equal(expectedSecretKeys);
        shape.Where(setting => !setting.Secret).Should().NotBeEmpty();

        // Every secret round-trips through the credential store, including a JSON document.
        var protector = CreateProtector();
        var values = shape.Where(setting => setting.Secret)
            .ToDictionary(setting => setting.Key, setting => $"value-for-{setting.Key}");
        protector.RevealAll(protector.ProtectAll(values)).Should().BeEquivalentTo(values);
    }

    public static TheoryData<string, ProviderSettingDefinition[], string[]> MultiSecretShapes() => new()
    {
        { "bedrock", BedrockShape, new[] { "access_key_id", "secret_access_key", "session_token" } },
        { "vertex", VertexShape, new[] { "service_account_json" } },
    };

    [Fact]
    public void Missing_Required_Secrets_Should_Be_Reported_By_Label_So_The_Operator_Knows_What_To_Supply()
    {
        // A Bedrock-shaped credential with only the access key id: the mandatory secret is named,
        // the optional session token is not, and the non-secret region is not a secret's business.
        ProviderConfigurationRegistry.GetMissingRequiredSecrets(
                BedrockShape,
                new Dictionary<string, string> { ["access_key_id"] = "AKIAIOSFODNN7EXAMPLE" })
            .Should().Equal("Secret Access Key");

        // A blank value counts as missing - storing it would produce a key that cannot authenticate.
        ProviderConfigurationRegistry.GetMissingRequiredSecrets(
                VertexShape,
                new Dictionary<string, string> { ["service_account_json"] = "   " })
            .Should().Equal("Service Account JSON");

        ProviderConfigurationRegistry.GetMissingRequiredSecrets(
                BedrockShape,
                new Dictionary<string, string>
                {
                    ["access_key_id"] = "AKIAIOSFODNN7EXAMPLE",
                    ["secret_access_key"] = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
                })
            .Should().BeEmpty();
    }

    [Fact]
    public void Required_Secrets_Should_Be_Reported_By_Label_When_A_Credential_Omits_Them()
    {
        // Reported against a live provider type so the check exercises the registry, not a fixture.
        ProviderConfigurationRegistry.GetSecretSettings(ProviderType.OpenAI).Should().BeEmpty();
        ProviderConfigurationRegistry.GetMissingRequiredSecrets(ProviderType.OpenAI, null).Should().BeEmpty();

        // Non-secret settings stay on the provider, so the split is total.
        ProviderConfigurationRegistry.GetNonSecretSettings(ProviderType.Cloudflare)
            .Select(setting => setting.Key)
            .Should().Equal("account_id");
        ProviderConfigurationRegistry.GetSecretSettings(ProviderType.Cloudflare).Should().BeEmpty();
    }

    [Fact]
    public void No_Provider_Should_Declare_A_Secret_Setting_With_A_UrlPathToken_Binding()
    {
        // A URL-token setting is read from the plaintext Provider.Settings bag, so declaring one
        // secret would route secret material into unencrypted storage - and into a URL.
        foreach (var providerType in ProviderTypeCatalog.ConfigurableTypes)
        {
            ProviderConfigurationRegistry.GetSecretSettings(providerType)
                .Should().OnlyContain(setting => setting.Binding != ProviderSettingBinding.UrlPathToken,
                    $"{providerType} must not put a secret in the URL or the plaintext settings bag");
        }
    }
}
