using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Decorators;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers;
using ConduitLLM.Providers.OpenAI;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Providers
{
    public class DatabaseAwareLLMClientFactoryTests
    {
        private readonly Mock<IProviderService> _mockCredentialService;
        private readonly Mock<IModelProviderMappingService> _mockMappingService;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<DatabaseAwareLLMClientFactory>> _mockLogger;
        private readonly DatabaseAwareLLMClientFactory _factory;

        public DatabaseAwareLLMClientFactoryTests()
        {
            _mockCredentialService = new Mock<IProviderService>();
            _mockMappingService = new Mock<IModelProviderMappingService>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<DatabaseAwareLLMClientFactory>>();

            _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var mockServiceProvider = new Mock<IServiceProvider>();

            _factory = new DatabaseAwareLLMClientFactory(
                _mockCredentialService.Object,
                _mockMappingService.Object,
                _mockLoggerFactory.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object,
                mockServiceProvider.Object);
        }

        [Fact]
        public async Task GetClientAsync_WithNonExistentModel_ThrowsModelNotFoundException()
        {
            // Arrange
            var modelName = "non-existent-model";
            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync(modelName))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ModelNotFoundException>(
                async () => await _factory.GetClientAsync(modelName)
            );

            Assert.Equal($"Model '{modelName}' not found. Please check your model configuration.", exception.Message);
            Assert.Equal(modelName, exception.ModelName);
        }

        [Fact]
        public async Task GetClientAsync_WithDisabledProvider_ThrowsServiceUnavailableException()
        {
            // Arrange
            var modelName = "test-model";
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelName,
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            var provider = new Provider
            {
                Id = 1,
                ProviderName = "TestProvider",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = false // Disabled provider
            };

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync(modelName))
                .ReturnsAsync(mapping);

            _mockCredentialService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(provider);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
                async () => await _factory.GetClientAsync(modelName)
            );

            Assert.Equal($"Provider 'TestProvider' is currently disabled.", exception.Message);
            Assert.Equal("TestProvider", exception.ServiceName);
        }

        [Fact]
        public async Task GetClientAsync_WithNoApiKey_ThrowsConfigurationException()
        {
            // Arrange
            var modelName = "test-model";
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelName,
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            var provider = new Provider
            {
                Id = 1,
                ProviderName = "TestProvider",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync(modelName))
                .ReturnsAsync(mapping);

            _mockCredentialService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(provider);

            // Return empty list of key credentials
            _mockCredentialService.Setup(x => x.GetKeyCredentialsByProviderIdAsync(1))
                .ReturnsAsync(new List<ProviderKeyCredential>());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ConfigurationException>(
                async () => await _factory.GetClientAsync(modelName)
            );

            Assert.Contains("No API key configured", exception.Message);
        }

        [Fact]
        public async Task GetClientByProviderIdAsync_WithNonExistentProvider_ThrowsInvalidRequestException()
        {
            // Arrange
            var providerId = 999;
            _mockCredentialService.Setup(x => x.GetProviderByIdAsync(providerId))
                .ReturnsAsync((Provider?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidRequestException>(
                async () => await _factory.GetClientByProviderIdAsync(providerId)
            );

            Assert.Equal($"Provider with ID '{providerId}' not found.", exception.Message);
            Assert.Equal("provider_not_found", exception.ErrorCode);
            Assert.Equal("providerId", exception.Param);
        }

        [Fact]
        public async Task GetClientByProviderIdAsync_WithProviderModelId_DoesNotResolveModelAlias()
        {
            // Arrange - provider exists but has no key, so client creation stops after the
            // provider lookup; the mapping service must never be consulted on this path
            var providerId = 1;
            var provider = new Provider
            {
                Id = providerId,
                ProviderName = "TestProvider",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };

            _mockCredentialService.Setup(x => x.GetProviderByIdAsync(providerId))
                .ReturnsAsync(provider);
            _mockCredentialService.Setup(x => x.GetKeyCredentialsByProviderIdAsync(providerId))
                .ReturnsAsync(new List<ProviderKeyCredential>());

            // Act & Assert
            await Assert.ThrowsAsync<ConfigurationException>(
                async () => await _factory.GetClientByProviderIdAsync(providerId, "gpt-image-1")
            );

            _mockMappingService.Verify(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetClientByProviderIdAsync_WithProviderModelIdAndNonExistentProvider_ThrowsInvalidRequestException()
        {
            // Arrange
            var providerId = 999;
            _mockCredentialService.Setup(x => x.GetProviderByIdAsync(providerId))
                .ReturnsAsync((Provider?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidRequestException>(
                async () => await _factory.GetClientByProviderIdAsync(providerId, "gpt-image-1")
            );

            Assert.Equal($"Provider with ID '{providerId}' not found.", exception.Message);
            Assert.Equal("provider_not_found", exception.ErrorCode);
            Assert.Equal("providerId", exception.Param);
        }

        [Fact]
        public async Task GetClientByProviderTypeAsync_WithNoProvider_ThrowsInvalidRequestException()
        {
            // Arrange
            var providerType = ProviderType.OpenAI;
            _mockCredentialService.Setup(x => x.GetAllProvidersAsync())
                .ReturnsAsync(new List<Provider>());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidRequestException>(
                async () => await _factory.GetClientByProviderTypeAsync(providerType)
            );

            Assert.Equal($"No provider configured for type '{providerType}'.", exception.Message);
            Assert.Equal("provider_type_not_found", exception.ErrorCode);
            Assert.Equal("providerType", exception.Param);
        }

        [Fact]
        public void CreateTestClient_WithNullProvider_ThrowsArgumentNullException()
        {
            var credential = new ProviderKeyCredential { ApiKey = "test-key" };

            var exception = Assert.Throws<ArgumentNullException>(
                () => _factory.CreateTestClient(null, credential));

            Assert.Equal("provider", exception.ParamName);
        }

        [Fact]
        public void CreateTestClient_WithNullCredential_ThrowsArgumentNullException()
        {
            var provider = new Provider { ProviderType = ProviderType.OpenAI };

            var exception = Assert.Throws<ArgumentNullException>(
                () => _factory.CreateTestClient(provider, null));

            Assert.Equal("keyCredential", exception.ParamName);
        }

        [Fact]
        public void CreateTestClient_WithBlankApiKey_ThrowsArgumentException()
        {
            var provider = new Provider { ProviderType = ProviderType.OpenAI };
            var credential = new ProviderKeyCredential { ApiKey = " " };

            var exception = Assert.Throws<ArgumentException>(
                () => _factory.CreateTestClient(provider, credential));

            Assert.Equal("keyCredential", exception.ParamName);
        }

        [Fact]
        public void CreateTestClient_WithSupportedProvider_UsesSuppliedCredentialWithoutDatabaseLookup()
        {
            var provider = new Provider
            {
                Id = 17,
                ProviderName = "CredentialTest",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };
            var credential = new ProviderKeyCredential
            {
                Id = 23,
                ProviderId = provider.Id,
                ApiKey = "test-key",
                IsEnabled = true
            };

            var client = _factory.CreateTestClient(provider, credential);

            var contextClient = Assert.IsType<ContextAwareLLMClient>(client);
            Assert.IsType<OpenAIClient>(contextClient.InnerClient);
            _mockCredentialService.VerifyNoOtherCalls();
            _mockMappingService.VerifyNoOtherCalls();
        }

        [Fact]
        public void CreateTestClient_WithUnsupportedProvider_ThrowsConfigurationException()
        {
            var provider = new Provider { ProviderType = (ProviderType)int.MaxValue };
            var credential = new ProviderKeyCredential { ApiKey = "test-key" };

            var exception = Assert.Throws<ConfigurationException>(
                () => _factory.CreateTestClient(provider, credential));

            Assert.Contains("Unsupported provider type", exception.Message);
        }
    }
}
