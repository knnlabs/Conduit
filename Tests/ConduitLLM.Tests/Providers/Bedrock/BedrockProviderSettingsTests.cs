using System.Text.RegularExpressions;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Configuration;

using FluentAssertions;

using Xunit;

namespace ConduitLLM.Tests.Providers.Bedrock;

/// <summary>
/// Amazon Bedrock is the first provider to consume the multi-secret credential mechanism from
/// epic #1177: the region is a provider-level structured setting that builds the endpoint and
/// scopes SigV4 signing, and the secret access key / session token are secret-typed settings
/// living encrypted on the key credential.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class BedrockProviderSettingsTests
{
    private static Provider BedrockProvider(Dictionary<string, string>? settings = null) => new()
    {
        ProviderType = ProviderType.Bedrock,
        ProviderName = "bedrock",
        Settings = settings
    };

    [Fact]
    public void Bedrock_Should_Be_A_Configurable_Provider_Type_With_Registered_Adapter_Defaults()
    {
        ProviderTypeCatalog.IsConfigurable(ProviderType.Bedrock).Should().BeTrue();
        ProviderConfigurationRegistry.TryGetConfiguration(ProviderType.Bedrock, out var config).Should().BeTrue();
        ClientCreatorRegistry.IsSupported(ProviderType.Bedrock).Should().BeTrue();
    }

    [Fact]
    public void Bedrock_Should_Declare_Region_As_A_Required_Url_Token_Setting()
    {
        ProviderConfigurationRegistry.TryGetConfiguration(ProviderType.Bedrock, out var config).Should().BeTrue();

        var region = config!.Settings.Should().ContainSingle(s => s.Key == "region").Subject;
        region.Required.Should().BeTrue();
        region.Secret.Should().BeFalse();
        region.Binding.Should().Be(ProviderSettingBinding.UrlPathToken);
        region.EffectiveBindingTarget.Should().Be("region");

        Regex.IsMatch("us-east-1", region.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("us-gov-west-1", region.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("ap-southeast-2", region.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("not a region", region.ValidationRegex!).Should().BeFalse();
        Regex.IsMatch("useast1", region.ValidationRegex!).Should().BeFalse();
    }

    [Fact]
    public void Bedrock_Should_Declare_Secret_Access_Key_And_Session_Token_As_Secret_Settings()
    {
        var secrets = ProviderConfigurationRegistry.GetSecretSettings(ProviderType.Bedrock);

        secrets.Should().HaveCount(2);
        secrets.Should().OnlyContain(s => s.Secret && s.Binding == ProviderSettingBinding.AuthScope);

        // Both are optional at the declaration level: the Bearer (Bedrock API key) mode uses
        // neither, and the client validates the SigV4 pairing itself with an actionable error.
        secrets.Should().ContainSingle(s => s.Key == "secret_access_key").Which.Required.Should().BeFalse();
        secrets.Should().ContainSingle(s => s.Key == "session_token").Which.Required.Should().BeFalse();
    }

    [Fact]
    public void Secret_Settings_Must_Never_Appear_In_The_Plaintext_Settings_Schema()
    {
        var nonSecret = ProviderConfigurationRegistry.GetNonSecretSettings(ProviderType.Bedrock);

        nonSecret.Should().ContainSingle().Which.Key.Should().Be("region");
    }

    [Fact]
    public void ResolveBaseUrl_Should_Substitute_The_Region_Into_The_Default_Endpoint()
    {
        var provider = BedrockProvider(new Dictionary<string, string> { ["region"] = "eu-central-1" });

        ProviderConfigurationRegistry.ResolveBaseUrl(provider)
            .Should().Be("https://bedrock-runtime.eu-central-1.amazonaws.com");
    }

    [Fact]
    public void ResolveBaseUrl_Without_A_Region_Should_Raise_An_Actionable_Error()
    {
        var act = () => ProviderConfigurationRegistry.ResolveBaseUrl(BedrockProvider());

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*AWS Region*");
    }

    [Fact]
    public void An_Explicit_Base_Url_Override_Should_Still_Win()
    {
        var provider = BedrockProvider(new Dictionary<string, string> { ["region"] = "us-east-1" });
        provider.BaseUrl = "https://bedrock-proxy.internal.example.com";

        ProviderConfigurationRegistry.ResolveBaseUrl(provider)
            .Should().Be("https://bedrock-proxy.internal.example.com");
    }
}
