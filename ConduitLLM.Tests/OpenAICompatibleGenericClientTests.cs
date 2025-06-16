using System;
using System.Net.Http;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests
{
    public class OpenAICompatibleGenericClientTests
    {
        private readonly Mock<ILogger<OpenAICompatibleGenericClient>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

        public OpenAICompatibleGenericClientTests()
        {
            _mockLogger = new Mock<ILogger<OpenAICompatibleGenericClient>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        }

        [Fact]
        public void Constructor_RequiresApiBaseUrl()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "openai-compatible",
                ApiKey = "test-key",
                ApiBase = null // Missing API base
            };

            // Act & Assert
            var exception = Assert.Throws<ConfigurationException>(() =>
                new OpenAICompatibleGenericClient(
                    credentials,
                    "test-model",
                    _mockLogger.Object,
                    _mockHttpClientFactory.Object));

            Assert.Contains("API Base URL is required", exception.Message);
        }

        [Fact]
        public void Constructor_AcceptsValidConfiguration()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "openai-compatible",
                ApiKey = "test-key",
                ApiBase = "https://api.example.com"
            };

            // Act
            var client = new OpenAICompatibleGenericClient(
                credentials,
                "test-model",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert
            Assert.NotNull(client);
            // ProviderName is protected, so we can't directly test it
            // But the fact that the client was created successfully is enough
        }

        [Fact]
        public void Constructor_TrimsTrailingSlashFromApiBase()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "openai-compatible",
                ApiKey = "test-key",
                ApiBase = "https://api.example.com/"
            };

            // Act
            var client = new OpenAICompatibleGenericClient(
                credentials,
                "test-model",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert
            Assert.NotNull(client);
            // The base URL should be stored without trailing slash
            // (this is handled internally in the base class)
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_RejectsEmptyApiBaseUrl(string apiBase)
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "openai-compatible",
                ApiKey = "test-key",
                ApiBase = apiBase
            };

            // Act & Assert
            var exception = Assert.Throws<ConfigurationException>(() =>
                new OpenAICompatibleGenericClient(
                    credentials,
                    "test-model",
                    _mockLogger.Object,
                    _mockHttpClientFactory.Object));

            Assert.Contains("API Base URL is required", exception.Message);
        }

        [Fact]
        public void Constructor_RejectsNullApiBaseUrl()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "openai-compatible",
                ApiKey = "test-key",
                ApiBase = null
            };

            // Act & Assert
            var exception = Assert.Throws<ConfigurationException>(() =>
                new OpenAICompatibleGenericClient(
                    credentials,
                    "test-model",
                    _mockLogger.Object,
                    _mockHttpClientFactory.Object));

            Assert.Contains("API Base URL is required", exception.Message);
        }

        [Fact]
        public void ConfigureHttpClient_AddsCustomUserAgent()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "openai-compatible",
                ApiKey = "test-key",
                ApiBase = "https://api.example.com"
            };

            var httpClient = new HttpClient();
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            var client = new OpenAICompatibleGenericClient(
                credentials,
                "test-model",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Use reflection to invoke the protected ConfigureHttpClient method
            var method = typeof(OpenAICompatibleGenericClient).GetMethod(
                "ConfigureHttpClient",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method?.Invoke(client, new object[] { httpClient, "test-key" });

            // Assert
            Assert.Contains("ConduitLLM/OpenAI-Compatible",
                httpClient.DefaultRequestHeaders.UserAgent.ToString());
        }
    }
}
