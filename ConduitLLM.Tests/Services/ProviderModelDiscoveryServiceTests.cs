using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Tests for the ProviderModelDiscoveryService implementations.
    /// </summary>
    public class ProviderModelDiscoveryServiceTests
    {
        private readonly Mock<ILogger<ProviderModelDiscoveryService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly HttpClient _httpClient;

        public ProviderModelDiscoveryServiceTests()
        {
            _mockLogger = new Mock<ILogger<ProviderModelDiscoveryService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        #region SupportsDiscovery Tests

        [Theory]
        [InlineData(ProviderType.OpenAI, true)]
        [InlineData(ProviderType.Groq, true)]
        [InlineData(ProviderType.Anthropic, true)]
        [InlineData(ProviderType.OpenRouter, true)]
        [InlineData(ProviderType.Cerebras, true)]
        [InlineData(ProviderType.Gemini, true)]
        [InlineData(ProviderType.MiniMax, true)]
        [InlineData(ProviderType.Replicate, true)]
        [InlineData(ProviderType.Mistral, true)]
        [InlineData(ProviderType.Cohere, true)]
        [InlineData(ProviderType.AzureOpenAI, true)]
        [InlineData(ProviderType.Bedrock, true)]
        [InlineData(ProviderType.VertexAI, true)]
        [InlineData(ProviderType.Ollama, true)]
        [InlineData(ProviderType.Fireworks, true)]
        [InlineData(ProviderType.HuggingFace, true)]
        [InlineData(ProviderType.SageMaker, true)]
        [InlineData(ProviderType.OpenAICompatible, true)]
        public void SupportsDiscovery_ReturnsCorrectValue(ProviderType providerType, bool expected)
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act
            var result = service.SupportsDiscovery(providerType);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SupportsDiscovery_ForKnownProviders_ReturnsTrue()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act & Assert
            Assert.True(service.SupportsDiscovery(ProviderType.OpenAI));
            Assert.True(service.SupportsDiscovery(ProviderType.Anthropic));
            Assert.True(service.SupportsDiscovery(ProviderType.Groq));
            Assert.True(service.SupportsDiscovery(ProviderType.MiniMax));
        }

        #endregion

        #region DiscoverModelsAsync Tests

        [Theory]
        [InlineData(ProviderType.OpenAI)]
        [InlineData(ProviderType.Anthropic)]
        [InlineData(ProviderType.Gemini)]
        [InlineData(ProviderType.Groq)]
        [InlineData(ProviderType.MiniMax)]
        public async Task DiscoverModelsAsync_WithValidProvider_CallsCorrectDiscoveryClass(ProviderType providerType)
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var providerCredential = new ProviderCredential
            {
                Id = 1,
                ProviderType = providerType,
                ProviderName = providerType.ToString().ToLower()
            };

            // Act
            var models = await service.DiscoverModelsAsync(providerCredential, _httpClient, null);

            // Assert
            Assert.NotNull(models);
            Assert.IsType<System.Collections.Generic.List<DiscoveredModel>>(models);
            
            // Even without API key, some providers return known models
            if (providerType == ProviderType.Anthropic || providerType == ProviderType.OpenAI)
            {
                Assert.Empty(models); // These require API key
            }
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithUnsupportedProvider_ReturnsEmptyList()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var providerCredential = new ProviderCredential
            {
                Id = 1,
                ProviderType = (ProviderType)999, // Invalid provider type
                ProviderName = "unsupported-provider"
            };

            // Act
            var models = await service.DiscoverModelsAsync(providerCredential, _httpClient, "api-key");

            // Assert
            Assert.Empty(models);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No provider-specific discovery available")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DiscoverModelsAsync_SetsProviderNameCorrectly()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var providerCredential = new ProviderCredential
            {
                Id = 1,
                ProviderType = ProviderType.Anthropic,
                ProviderName = "anthropic"
            };

            // Act - use anthropic which returns static models when API key is provided
            var models = await service.DiscoverModelsAsync(providerCredential, _httpClient, "test-key");

            // Assert
            Assert.NotEmpty(models);
            Assert.All(models, m => Assert.Equal("anthropic", m.Provider));
        }

        [Fact]
        public async Task DiscoverModelsAsync_HandlesUnsupportedProviderGracefully()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var providerCredential = new ProviderCredential
            {
                Id = 1,
                ProviderType = (ProviderType)999, // Invalid provider type
                ProviderName = "unsupported-provider"
            };
            
            // Act - Use an unsupported provider
            var models = await service.DiscoverModelsAsync(providerCredential, _httpClient, "test-key");

            // Assert
            Assert.NotNull(models);
            Assert.Empty(models); // Should return empty list for unsupported provider
            
            // Verify debug log for unsupported provider
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No provider-specific discovery available")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Provider Alias Tests

        [Theory]
        [InlineData(ProviderType.GoogleCloud, ProviderType.Gemini)] // Both should work
        public async Task DiscoverModelsAsync_HandlesProviderAliases(ProviderType provider1, ProviderType provider2)
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var providerCredential1 = new ProviderCredential
            {
                Id = 1,
                ProviderType = provider1,
                ProviderName = provider1.ToString().ToLower()
            };
            var providerCredential2 = new ProviderCredential
            {
                Id = 2,
                ProviderType = provider2,
                ProviderName = provider2.ToString().ToLower()
            };

            // Act
            var models1 = await service.DiscoverModelsAsync(providerCredential1, _httpClient, "key");
            var models2 = await service.DiscoverModelsAsync(providerCredential2, _httpClient, "key");

            // Assert
            // Note: Google and Gemini are now distinct provider types, so they may return different models
            // This test just verifies that both providers are supported
            Assert.NotNull(models1);
            Assert.NotNull(models2);
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task DiscoverModelsAsync_LogsDiscoveryProcess()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var providerCredential = new ProviderCredential
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                ProviderName = "openai"
            };

            // Act
            await service.DiscoverModelsAsync(providerCredential, _httpClient, "test-key");

            // Assert
            // Should log the start of discovery
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Calling OpenAIModelDiscovery.DiscoverAsync")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            // Should log the result
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("returned") && v.ToString().Contains("models")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}