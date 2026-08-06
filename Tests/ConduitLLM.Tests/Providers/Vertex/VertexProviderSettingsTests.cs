using System.Text.RegularExpressions;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Configuration;

using AwesomeAssertions;

using Xunit;

namespace ConduitLLM.Tests.Providers.Vertex;

[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class VertexProviderSettingsTests
{
    private static Provider VertexProvider(Dictionary<string, string>? settings = null) => new()
    {
        ProviderType = ProviderType.Vertex,
        ProviderName = "vertex",
        Settings = settings
    };

    [Fact]
    public void Vertex_Should_Be_A_Configurable_Provider_Type_With_Registered_Adapter_Defaults()
    {
        ProviderTypeCatalog.IsConfigurable(ProviderType.Vertex).Should().BeTrue();
        ProviderConfigurationRegistry.TryGetConfiguration(ProviderType.Vertex, out var config).Should().BeTrue();
        ClientCreatorRegistry.IsSupported(ProviderType.Vertex).Should().BeTrue();
        config!.AuthenticationStrategy.RequiresApiKey.Should().BeFalse();
    }

    [Fact]
    public void Vertex_Should_Declare_Project_And_Location_As_Required_Url_Settings()
    {
        var settings = ProviderConfigurationRegistry.GetNonSecretSettings(ProviderType.Vertex);

        settings.Select(setting => setting.Key)
            .Should().Equal("project_id", "location");
        settings.Should().OnlyContain(setting =>
            setting.Required
            && !setting.Secret
            && setting.Binding == ProviderSettingBinding.UrlPathToken);

        var project = settings.Single(setting => setting.Key == "project_id");
        Regex.IsMatch("my-vertex-project", project.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("INVALID_PROJECT", project.ValidationRegex!).Should().BeFalse();

        var location = settings.Single(setting => setting.Key == "location");
        Regex.IsMatch("us-central1", location.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("europe-west4", location.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("global", location.ValidationRegex!).Should().BeTrue();
        Regex.IsMatch("not a location", location.ValidationRegex!).Should().BeFalse();
    }

    [Fact]
    public void Vertex_Should_Declare_Service_Account_Json_As_A_Required_Secret()
    {
        var secret = ProviderConfigurationRegistry.GetSecretSettings(ProviderType.Vertex)
            .Should().ContainSingle().Subject;

        secret.Key.Should().Be("service_account_json");
        secret.Required.Should().BeTrue();
        secret.Secret.Should().BeTrue();
        secret.Binding.Should().Be(ProviderSettingBinding.AuthScope);
    }

    [Fact]
    public void ResolveBaseUrl_Should_Substitute_Project_And_Location()
    {
        var provider = VertexProvider(new Dictionary<string, string>
        {
            ["project_id"] = "my-vertex-project",
            ["location"] = "us-central1"
        });

        ProviderConfigurationRegistry.ResolveBaseUrl(provider).Should().Be(
            "https://aiplatform.googleapis.com/v1beta1/projects/my-vertex-project"
            + "/locations/us-central1/endpoints/openapi");
    }

    [Fact]
    public void ResolveBaseUrl_Without_Project_Should_Raise_An_Actionable_Error()
    {
        var provider = VertexProvider(new Dictionary<string, string>
        {
            ["location"] = "us-central1"
        });

        var action = () => ProviderConfigurationRegistry.ResolveBaseUrl(provider);

        action.Should().Throw<ConfigurationException>()
            .WithMessage("*Google Cloud Project ID*");
    }
}
