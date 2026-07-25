using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration;
using ConduitLLM.Providers.Configuration;

using FluentAssertions;

using Microsoft.AspNetCore.Http.HttpResults;

namespace ConduitLLM.Tests.Admin.Endpoints;

/// <summary>
/// Covers the settings-schema projection that lets administrative UIs render provider settings
/// straight from the backend registry instead of a hand-maintained client-side copy.
/// </summary>
public class ProviderSettingsSchemaEndpointTests
{
    private static IReadOnlyList<ProviderSettingsSchemaDto> GetSchema()
    {
        var result = ProviderCredentialsEndpoints.GetProviderSettingsSchema();
        return result.Should().BeOfType<Ok<ProviderSettingsSchemaDto[]>>().Subject.Value!;
    }

    [Fact]
    public void Schema_Should_Project_Every_Declared_Setting_From_The_Registry()
    {
        var schema = GetSchema();

        foreach (var providerType in ProviderTypeCatalog.ConfigurableTypes)
        {
            var declared = ProviderConfigurationRegistry.GetConfiguration(providerType)?.Settings
                ?? (IReadOnlyList<ProviderSettingDefinition>)Array.Empty<ProviderSettingDefinition>();
            var published = schema.SingleOrDefault(entry => entry.ProviderType == providerType);

            if (declared.Count == 0)
            {
                published.Should().BeNull($"{providerType} declares no settings and should be omitted");
                continue;
            }

            published.Should().NotBeNull($"{providerType} declares settings that clients must render");
            published!.Settings.Select(setting => setting.Key)
                .Should().Equal(declared.Select(definition => definition.Key));
        }
    }

    [Fact]
    public void Schema_Should_Publish_The_Cloudflare_AccountId_Field_With_Its_Label_And_Validation()
    {
        var cloudflare = GetSchema()
            .Should().ContainSingle(entry => entry.ProviderType == ProviderType.Cloudflare).Subject;

        var accountId = cloudflare.Settings.Should().ContainSingle(setting => setting.Key == "account_id").Subject;
        accountId.Label.Should().Be("Account ID");
        accountId.Required.Should().BeTrue();
        accountId.Secret.Should().BeFalse();
        accountId.ValidationRegex.Should().Be("^[0-9a-fA-F]{32}$");
        accountId.HelpText.Should().NotBeNullOrWhiteSpace();
        accountId.Placeholder.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Schema_Should_Publish_Renderable_Fields_With_Unique_Keys()
    {
        // The key is both the storage key and the form field path, so a blank or duplicated key
        // would silently collapse two operator inputs into one stored value.
        foreach (var entry in GetSchema())
        {
            entry.Settings.Should().NotBeEmpty();
            entry.Settings.Should().OnlyContain(setting =>
                !string.IsNullOrWhiteSpace(setting.Key) && !string.IsNullOrWhiteSpace(setting.Label));
            entry.Settings.Select(setting => setting.Key)
                .Should().OnlyHaveUniqueItems($"{entry.ProviderType} settings must not collide");
        }
    }
}
