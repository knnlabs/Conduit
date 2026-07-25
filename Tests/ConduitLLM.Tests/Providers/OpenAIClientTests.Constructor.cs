using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.OpenAI;

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
                ProviderType = ProviderType.Azure,
                ProviderName = "azure",
                Settings = new Dictionary<string, string> { ["resource_name"] = "myinstance" }
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
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

        [Fact]
        public void Constructor_ForAzureWithoutResourceName_ThrowsActionableConfigurationException()
        {
            // Azure cannot be addressed without a resource: the registry raises a named configuration
            // error rather than letting a request go to an unsubstituted {resource_name} host.
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Azure,
                ProviderName = "azure"
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var logger = CreateLogger<OpenAIClient>();

            var act = () => new OpenAIClient(
                provider,
                keyCredential,
                "my-deployment",
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            var ex = Assert.Throws<ConfigurationException>(act);
            Assert.Contains("Resource Name", ex.Message);
        }

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