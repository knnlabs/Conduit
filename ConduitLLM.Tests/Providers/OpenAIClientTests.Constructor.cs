using System;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.OpenAI;
using Xunit;

namespace ConduitLLM.Tests.Providers
{
    public partial class OpenAIClientTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidOpenAICredentials_InitializesCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var modelId = "gpt-4";
            var logger = CreateLogger<OpenAIClient>();

            // Act
            var client = new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithValidAzureCredentials_InitializesCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key",
                BaseUrl = "https://myinstance.openai.azure.com"
            };
            
            var modelId = "my-deployment";
            var logger = CreateLogger<OpenAIClient>();

            // Act
            var client = new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        // Test removed: Azure OpenAI provider type no longer supported
        // Original test: Constructor_WithAzureButNoApiBase_ThrowsConfigurationException
        // This test verified Azure OpenAI requires BaseUrl, but Azure OpenAI has been removed from supported providers

        [Fact]
        public void Constructor_WithNullCredentials_ThrowsException()
        {
            // Arrange
            var modelId = "gpt-4";
            var logger = CreateLogger<OpenAIClient>();

            // Act & Assert
            // The constructor will throw either NullReferenceException or ArgumentNullException
            // depending on the order of validation
            Assert.ThrowsAny<Exception>(() =>
                new OpenAIClient(
                    null!,
                    null!,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var provider = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key"
            };
            
            var modelId = "gpt-4";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new OpenAIClient(
                    provider,
                    keyCredential,
                    modelId,
                    null!,
                    _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_DoesNotThrow()
        {
            // Arrange
            var provider = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key"
            };
            
            var modelId = "gpt-4";
            var logger = CreateLogger<OpenAIClient>();

            // Act
            var client = new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                null); // HttpClientFactory is optional

            // Assert
            Assert.NotNull(client);
        }

        #endregion
    }
}