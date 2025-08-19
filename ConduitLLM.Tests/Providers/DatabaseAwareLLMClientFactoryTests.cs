using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
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
            
            _factory = new DatabaseAwareLLMClientFactory(
                _mockCredentialService.Object,
                _mockMappingService.Object,
                _mockLoggerFactory.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void GetClient_WithNonExistentModel_ThrowsModelNotFoundException()
        {
            // Arrange
            var modelName = "non-existent-model";
            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync(modelName))
                .ReturnsAsync((ModelProviderMapping?)null);
            
            // Act & Assert
            var exception = Assert.Throws<ModelNotFoundException>(
                () => _factory.GetClient(modelName)
            );
            
            Assert.Equal($"Model '{modelName}' not found. Please check your model configuration.", exception.Message);
            Assert.Equal(modelName, exception.ModelName);
        }

        [Fact]
        public void GetClient_WithDisabledProvider_ThrowsServiceUnavailableException()
        {
            // Arrange
            var modelName = "test-model";
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelName,
                    ModelId = 1,
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
            var exception = Assert.Throws<ServiceUnavailableException>(
                () => _factory.GetClient(modelName)
            );
            
            Assert.Equal($"Provider 'TestProvider' is currently disabled.", exception.Message);
            Assert.Equal("TestProvider", exception.ServiceName);
        }

        [Fact]
        public void GetClient_WithNoApiKey_ThrowsConfigurationException()
        {
            // Arrange
            var modelName = "test-model";
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = modelName,
                    ModelId = 1,
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
            var exception = Assert.Throws<ConfigurationException>(
                () => _factory.GetClient(modelName)
            );
            
            Assert.Contains("No API key configured", exception.Message);
        }

        [Fact]
        public void GetClientByProviderId_WithNonExistentProvider_ThrowsInvalidRequestException()
        {
            // Arrange
            var providerId = 999;
            _mockCredentialService.Setup(x => x.GetProviderByIdAsync(providerId))
                .ReturnsAsync((Provider?)null);
            
            // Act & Assert
            var exception = Assert.Throws<InvalidRequestException>(
                () => _factory.GetClientByProviderId(providerId)
            );
            
            Assert.Equal($"Provider with ID '{providerId}' not found.", exception.Message);
            Assert.Equal("provider_not_found", exception.ErrorCode);
            Assert.Equal("providerId", exception.Param);
        }

        [Fact]
        public void GetClientByProviderType_WithNoProvider_ThrowsInvalidRequestException()
        {
            // Arrange
            var providerType = ProviderType.OpenAI;
            _mockCredentialService.Setup(x => x.GetAllProvidersAsync())
                .ReturnsAsync(new List<Provider>());
            
            // Act & Assert
            var exception = Assert.Throws<InvalidRequestException>(
                () => _factory.GetClientByProviderType(providerType)
            );
            
            Assert.Equal($"No provider configured for type '{providerType}'.", exception.Message);
            Assert.Equal("provider_type_not_found", exception.ErrorCode);
            Assert.Equal("providerType", exception.Param);
        }
    }
}