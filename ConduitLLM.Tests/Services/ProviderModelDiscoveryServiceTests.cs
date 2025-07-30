using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
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
        [InlineData("openai", true)]
        [InlineData("groq", true)]
        [InlineData("anthropic", true)]
        [InlineData("openrouter", true)]
        [InlineData("cerebras", true)]
        [InlineData("google", true)]
        [InlineData("gemini", true)]
        [InlineData("minimax", true)]
        [InlineData("replicate", true)]
        [InlineData("mistral", true)]
        [InlineData("cohere", true)]
        [InlineData("azureopenai", true)]
        [InlineData("bedrock", true)]
        [InlineData("vertexai", true)]
        [InlineData("ollama", true)]
        [InlineData("fireworks", true)]
        [InlineData("huggingface", true)]
        [InlineData("sagemaker", true)]
        [InlineData("openaicompatible", true)]
        [InlineData("unknown-provider", false)]
        [InlineData("custom", false)]
        [InlineData("", false)]
        public void SupportsDiscovery_ReturnsCorrectValue(string providerName, bool expected)
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act
            var result = service.SupportsDiscovery(providerName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SupportsDiscovery_IsCaseInsensitive()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act & Assert
            Assert.True(service.SupportsDiscovery("OpenAI"));
            Assert.True(service.SupportsDiscovery("OPENAI"));
            Assert.True(service.SupportsDiscovery("openai"));
            Assert.True(service.SupportsDiscovery("OpEnAi"));
        }

        #endregion

        #region DiscoverModelsAsync Tests

        [Theory]
        [InlineData("openai")]
        [InlineData("anthropic")]
        [InlineData("google")]
        [InlineData("groq")]
        [InlineData("minimax")]
        public async Task DiscoverModelsAsync_WithValidProvider_CallsCorrectDiscoveryClass(string providerName)
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act
            var models = await service.DiscoverModelsAsync(providerName, _httpClient, null);

            // Assert
            Assert.NotNull(models);
            Assert.IsType<System.Collections.Generic.List<DiscoveredModel>>(models);
            
            // Even without API key, some providers return known models
            if (providerName == "anthropic" || providerName == "openai")
            {
                Assert.Empty(models); // These require API key
            }
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithUnsupportedProvider_ReturnsEmptyList()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act
            var models = await service.DiscoverModelsAsync("unsupported-provider", _httpClient, "api-key");

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

            // Act
            var models = await service.DiscoverModelsAsync("google", _httpClient, "test-key");

            // Assert
            Assert.NotEmpty(models);
            Assert.All(models, m => Assert.Equal("google", m.Provider));
        }

        [Fact]
        public async Task DiscoverModelsAsync_HandlesExceptionsGracefully()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);
            var badHttpClient = new HttpClient(); // No base address will cause issues

            // Act
            var models = await service.DiscoverModelsAsync("openai", badHttpClient, "test-key");

            // Assert
            Assert.NotNull(models);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error discovering models")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Provider Alias Tests

        [Theory]
        [InlineData("google", "gemini")] // Both should work
        [InlineData("gemini", "google")]
        public async Task DiscoverModelsAsync_HandlesProviderAliases(string provider1, string provider2)
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act
            var models1 = await service.DiscoverModelsAsync(provider1, _httpClient, "key");
            var models2 = await service.DiscoverModelsAsync(provider2, _httpClient, "key");

            // Assert
            Assert.Equal(models1.Count, models2.Count);
            // Both aliases should return the same models
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task DiscoverModelsAsync_LogsDiscoveryProcess()
        {
            // Arrange
            var service = new ProviderModelDiscoveryService(_mockLogger.Object);

            // Act
            await service.DiscoverModelsAsync("openai", _httpClient, "test-key");

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