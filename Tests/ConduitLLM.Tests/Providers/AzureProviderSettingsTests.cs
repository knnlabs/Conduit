using System.Net;
using System.Text;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Authentication;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.OpenAI;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Moq.Protected;

using Xunit;

namespace ConduitLLM.Tests.Providers;

/// <summary>
/// Azure OpenAI is a first-class provider type whose resource and api-version are declared settings.
/// Previously the client detected Azure by string-matching the provider name - a branch nothing could
/// reach, since no ProviderType stringified to "azure" - and pinned the api-version in a constant
/// (issue #1186).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Providers")]
public class AzureProviderSettingsTests
{
    private static Provider AzureProvider(Dictionary<string, string>? settings = null) => new()
    {
        ProviderType = ProviderType.Azure,
        ProviderName = "azure",
        Settings = settings
    };

    [Fact]
    public void Azure_Should_Be_A_Configurable_Provider_Type_With_Registered_Adapter_Defaults()
    {
        ProviderTypeCatalog.IsConfigurable(ProviderType.Azure).Should().BeTrue();
        ProviderConfigurationRegistry.TryGetConfiguration(ProviderType.Azure, out var config).Should().BeTrue();
        config!.AuthenticationStrategy.Should().BeSameAs(ApiKeyHeaderStrategy.AzureInstance);
    }

    [Fact]
    public void Azure_Should_Declare_ResourceName_And_ApiVersion_Settings()
    {
        ProviderConfigurationRegistry.TryGetConfiguration(ProviderType.Azure, out var config).Should().BeTrue();

        var resource = config!.Settings.Should().ContainSingle(s => s.Key == "resource_name").Subject;
        resource.Required.Should().BeTrue();
        resource.Binding.Should().Be(ProviderSettingBinding.UrlPathToken);

        var apiVersion = config.Settings.Should().ContainSingle(s => s.Key == "api_version").Subject;
        apiVersion.Required.Should().BeFalse();
        apiVersion.Binding.Should().Be(ProviderSettingBinding.QueryParam);
        apiVersion.EffectiveBindingTarget.Should().Be("api-version");
        apiVersion.DefaultValue.Should().Be(ProviderConfigurationRegistry.AzureDefaultApiVersion);
    }

    [Fact]
    public void ResolveBaseUrl_Should_Build_The_Resource_Endpoint_From_The_Resource_Name()
    {
        var provider = AzureProvider(new Dictionary<string, string> { ["resource_name"] = "my-openai-resource" });

        ProviderConfigurationRegistry.ResolveBaseUrl(provider)
            .Should()
            .Be("https://my-openai-resource.openai.azure.com");
    }

    [Fact]
    public void ResolveBaseUrl_Should_Name_The_Missing_Resource_Setting()
    {
        var action = () => ProviderConfigurationRegistry.ResolveBaseUrl(AzureProvider());

        action.Should().Throw<ConfigurationException>().WithMessage("*Resource Name*");
    }

    [Fact]
    public void ResolveBaseUrl_Should_Honor_A_Custom_Endpoint_For_Private_Domains()
    {
        var provider = AzureProvider(new Dictionary<string, string> { ["resource_name"] = "my-openai-resource" });
        provider.BaseUrl = "https://openai.internal.example.test";

        ProviderConfigurationRegistry.ResolveBaseUrl(provider)
            .Should()
            .Be("https://openai.internal.example.test");
    }

    [Fact]
    public void ApiVersion_Should_Fall_Back_To_The_Declared_Default_And_Be_Overridable()
    {
        ProviderConfigurationRegistry.GetSettingValue(ProviderType.Azure, null, "api_version")
            .Should().Be(ProviderConfigurationRegistry.AzureDefaultApiVersion);

        ProviderConfigurationRegistry.GetSettingValue(
                ProviderType.Azure,
                new Dictionary<string, string> { ["api_version"] = "2025-01-01-preview" },
                "api_version")
            .Should().Be("2025-01-01-preview");
    }

    [Theory]
    [InlineData(null, ProviderConfigurationRegistry.AzureDefaultApiVersion)]
    [InlineData("2025-01-01-preview", "2025-01-01-preview")]
    public async Task Azure_Requests_Should_Target_The_Resource_With_The_Configured_ApiVersion(
        string? configuredApiVersion,
        string expectedApiVersion)
    {
        var settings = new Dictionary<string, string> { ["resource_name"] = "my-openai-resource" };
        if (configuredApiVersion != null)
        {
            settings["api_version"] = configuredApiVersion;
        }

        HttpRequestMessage? captured = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{ "data": [] }""", Encoding.UTF8, "application/json")
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));

        var client = new OpenAIClient(
            AzureProvider(settings),
            new ProviderKeyCredential { ApiKey = "azure-key", IsPrimary = true, IsEnabled = true },
            "my-deployment",
            NullLogger<OpenAIClient>.Instance,
            factory.Object);

        await client.GetModelsAsync();

        captured.Should().NotBeNull();
        captured!.RequestUri!.ToString()
            .Should()
            .Be($"https://my-openai-resource.openai.azure.com/openai/deployments?api-version={expectedApiVersion}");

        // Azure authenticates with the api-key header, not a Bearer token.
        captured.Headers.Contains("api-key").Should().BeTrue();
        captured.Headers.Authorization.Should().BeNull();
    }
}
