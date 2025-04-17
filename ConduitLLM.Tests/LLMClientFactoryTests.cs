using System;
using System.Collections.Generic;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace ConduitLLM.Tests;

public class LLMClientFactoryTests
{
    private readonly Mock<IOptions<ConduitSettings>> _mockSettingsOptions;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly ConduitSettings _settings;

    public LLMClientFactoryTests()
    {
        _mockSettingsOptions = new Mock<IOptions<ConduitSettings>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _settings = new ConduitSettings // Initialize with some default test data
        {
            ModelMappings = new List<ModelProviderMapping>
            {
                new() { ModelAlias = "openai-test", ProviderName = "OpenAI", DeploymentName = "gpt-4", ProviderModelId = "gpt-4" },
                new() { ModelAlias = "anthropic-test", ProviderName = "Anthropic", DeploymentName = "claude-3", ProviderModelId = "claude-3-opus-20240229" },
                new() { ModelAlias = "gemini-test", ProviderName = "Gemini", DeploymentName = "gemini-pro", ProviderModelId = "gemini-pro" },
                new() { ModelAlias = "cohere-test", ProviderName = "Cohere", DeploymentName = "command-r", ProviderModelId = "command-r" },
                new() { ModelAlias = "azure-test", ProviderName = "Azure", DeploymentName = "azure-gpt-4", ProviderModelId = "azure-gpt-4" }, // Assuming Azure uses OpenAI client internally
                new() { ModelAlias = "mistral-test", ProviderName = "Mistral", DeploymentName = "mistral-large", ProviderModelId = "mistral-large" }, // Assuming Mistral uses OpenAI client
                new() { ModelAlias = "no-creds-test", ProviderName = "NoCredsProvider", DeploymentName = "model", ProviderModelId = "model" },
                new() { ModelAlias = "unsupported-test", ProviderName = "UnsupportedProvider", DeploymentName = "model", ProviderModelId = "model" }
            },
            ProviderCredentials = new List<ProviderCredentials>
            {
                new() { ProviderName = "OpenAI", ApiKey = "sk-openai" },
                new() { ProviderName = "Anthropic", ApiKey = "sk-anthropic" },
                new() { ProviderName = "Gemini", ApiKey = "gemini-key" },
                new() { ProviderName = "Cohere", ApiKey = "cohere-key" },
                new() { ProviderName = "Azure", ApiKey = "azure-key", ApiBase = "https://my-azure.openai.azure.com/" },
                new() { ProviderName = "Mistral", ApiKey = "mistral-key", ApiBase = "https://api.mistral.ai/v1" },
                // No credentials for NoCredsProvider
                new() { ProviderName = "UnsupportedProvider", ApiKey = "unsupported-key" } // Creds exist, but provider is unsupported
            }
        };

        _mockSettingsOptions.Setup(o => o.Value).Returns(_settings);

        // Setup logger factory to return a mock logger for any type
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
                          .Returns(new Mock<ILogger>().Object);
    }

    private LLMClientFactory CreateFactory()
    {
        return new LLMClientFactory(_mockSettingsOptions.Object, _mockLoggerFactory.Object);
    }

    [Theory]
    [InlineData("openai-test", typeof(OpenAIClient))]
    [InlineData("anthropic-test", typeof(AnthropicClient))]
    [InlineData("gemini-test", typeof(GeminiClient))]
    [InlineData("cohere-test", typeof(CohereClient))]
    [InlineData("azure-test", typeof(OpenAIClient))] // Azure uses OpenAIClient internally
    [InlineData("mistral-test", typeof(OpenAIClient))] // Mistral uses OpenAIClient internally
    public void GetClient_ValidAlias_ReturnsCorrectClientType(string modelAlias, Type expectedClientType)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var client = factory.GetClient(modelAlias);

        // Assert
        Assert.NotNull(client);
        Assert.IsAssignableFrom(expectedClientType, client);
    }

    [Theory]
    [InlineData("OpenAI", typeof(OpenAIClient))]
    [InlineData("Anthropic", typeof(AnthropicClient))]
    [InlineData("Gemini", typeof(GeminiClient))]
    [InlineData("Cohere", typeof(CohereClient))]
    [InlineData("Azure", typeof(OpenAIClient))]
    [InlineData("Mistral", typeof(OpenAIClient))]
    public void GetClientByProvider_ValidName_ReturnsCorrectClientType(string providerName, Type expectedClientType)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var client = factory.GetClientByProvider(providerName);

        // Assert
        Assert.NotNull(client);
        Assert.IsAssignableFrom(expectedClientType, client);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetClient_InvalidAlias_ThrowsArgumentException(string? invalidAlias)
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        Assert.Throws<ArgumentException>("modelAlias", () => factory.GetClient(invalidAlias!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetClientByProvider_InvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        Assert.Throws<ArgumentException>("providerName", () => factory.GetClientByProvider(invalidName!));
    }

    [Fact]
    public void GetClient_AliasNotFound_ThrowsConfigurationException()
    {
        // Arrange
        var factory = CreateFactory();
        var nonExistentAlias = "non-existent-alias";

        // Act & Assert
        var ex = Assert.Throws<ConfigurationException>(() => factory.GetClient(nonExistentAlias));
        Assert.Contains($"No model mapping found for alias '{nonExistentAlias}'", ex.Message);
    }

     [Fact]
    public void GetClientByProvider_ProviderCredsNotFound_ThrowsConfigurationException()
    {
        // Arrange
        var factory = CreateFactory();
        var nonExistentProvider = "non-existent-provider";

        // Act & Assert
        var ex = Assert.Throws<ConfigurationException>(() => factory.GetClientByProvider(nonExistentProvider));
        Assert.Contains($"No provider credentials found for provider '{nonExistentProvider}'", ex.Message);
    }

    [Fact]
    public void GetClient_ProviderCredsNotFoundForAlias_ThrowsConfigurationException()
    {
        // Arrange
        var factory = CreateFactory();
        var aliasWithNoCreds = "no-creds-test"; // Mapping exists, but no credentials

        // Act & Assert
        var ex = Assert.Throws<ConfigurationException>(() => factory.GetClient(aliasWithNoCreds));
        Assert.Contains($"No provider credentials found for provider 'NoCredsProvider'", ex.Message);
    }

    [Fact]
    public void GetClient_UnsupportedProvider_ThrowsUnsupportedProviderException()
    {
        // Arrange
        var factory = CreateFactory();
        var aliasWithUnsupportedProvider = "unsupported-test"; // Mapping and Creds exist

        // Act & Assert
        var ex = Assert.Throws<UnsupportedProviderException>(() => factory.GetClient(aliasWithUnsupportedProvider));
        Assert.Contains("Provider 'UnsupportedProvider' is not currently supported", ex.Message);
    }

     [Fact]
    public void GetClientByProvider_UnsupportedProvider_ThrowsUnsupportedProviderException()
    {
        // Arrange
        var factory = CreateFactory();
        var unsupportedProviderName = "UnsupportedProvider"; // Creds exist

        // Act & Assert
        var ex = Assert.Throws<UnsupportedProviderException>(() => factory.GetClientByProvider(unsupportedProviderName));
        Assert.Contains($"Provider '{unsupportedProviderName}' is not currently supported", ex.Message);
    }

    [Fact]
    public void Constructor_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        _mockSettingsOptions.Setup(o => o.Value).Returns((ConduitSettings)null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("settingsOptions", () => new LLMClientFactory(_mockSettingsOptions.Object, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        // Settings are valid

        // Act & Assert
        Assert.Throws<ArgumentNullException>("loggerFactory", () => new LLMClientFactory(_mockSettingsOptions.Object, null!));
    }
}
