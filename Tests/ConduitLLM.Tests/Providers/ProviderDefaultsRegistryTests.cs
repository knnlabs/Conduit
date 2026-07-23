using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Providers;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers;
using ConduitLLM.Providers.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Providers;

public class ProviderDefaultsRegistryTests
{
    [Fact]
    public void AdapterDefaults_Should_Cover_Every_ProviderType()
    {
        ProviderAdapterDefaultsRegistry.GetRegisteredProviderTypes()
            .OrderBy(x => x)
            .Should()
            .Equal(Enum.GetValues<ProviderType>().OrderBy(x => x));
    }

    [Fact]
    public void ProviderConfigurations_Should_Cover_Every_ProviderType_And_Use_Canonical_Urls()
    {
        ProviderConfigurationRegistry.GetRegisteredProviderTypes()
            .OrderBy(x => x)
            .Should()
            .Equal(Enum.GetValues<ProviderType>().OrderBy(x => x));

        foreach (var providerType in Enum.GetValues<ProviderType>())
        {
            ProviderConfigurationRegistry.GetDefaultBaseUrl(providerType)
                .Should()
                .Be(ProviderAdapterDefaultsRegistry.GetRequired(providerType).DefaultBaseUrl);
        }
    }

    [Fact]
    public void OpenAICompatible_Should_Keep_Explicit_Database_Url_Requirement()
    {
        ProviderAdapterDefaultsRegistry.GetRequired(ProviderType.OpenAICompatible)
            .RequiresBaseUrlOverride
            .Should()
            .BeTrue();

        var provider = new Provider
        {
            ProviderType = ProviderType.OpenAICompatible,
            ProviderName = "custom"
        };
        var credential = new ProviderKeyCredential { ApiKey = "key" };
        var factory = Mock.Of<IHttpClientFactory>();

        var action = () => new OpenAICompatibleGenericClient(
            provider,
            credential,
            "model",
            NullLogger<OpenAICompatibleGenericClient>.Instance,
            factory);

        action.Should().Throw<ConfigurationException>()
            .WithMessage("*Base URL is required*");

        provider.BaseUrl = "http://localhost:11434/v1";
        action.Should().NotThrow();
    }

    [Fact]
    public void ResolveBaseUrl_Should_Prefer_And_Normalize_Database_Override()
    {
        var provider = new Provider
        {
            ProviderType = ProviderType.Groq,
            BaseUrl = "https://proxy.example.test/groq/"
        };

        ProviderConfigurationRegistry.ResolveBaseUrl(provider)
            .Should()
            .Be("https://proxy.example.test/groq");
    }
}
