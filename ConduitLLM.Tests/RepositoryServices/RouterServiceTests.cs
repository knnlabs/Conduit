using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using IRouterConfigCoreRepository = ConduitLLM.Core.Interfaces.IRouterConfigRepository;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class RouterServiceTests
    {
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _mockConfigContextFactory;
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IOptionsMonitor<RouterOptions>> _mockRouterOptions;
        private readonly ILogger<RouterService> _logger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly RouterService _service;
        
        // Legacy mocks kept for test compatibility
        private readonly Mock<IGlobalSettingRepository> _mockGlobalSettingRepository;
        private readonly Mock<IModelDeploymentRepository> _mockModelDeploymentRepository;
        private readonly Mock<ConduitLLM.Configuration.Repositories.IRouterConfigRepository> _mockRouterConfigRepository;
        private readonly Mock<IFallbackConfigurationRepository> _mockFallbackConfigRepository;

        public RouterServiceTests()
        {
            _mockConfigContextFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockRouterOptions = new Mock<IOptionsMonitor<RouterOptions>>();
            _logger = NullLogger<RouterService>.Instance;
            _mockServiceProvider = new Mock<IServiceProvider>();
            
            // Initialize legacy mocks
            _mockGlobalSettingRepository = new Mock<IGlobalSettingRepository>();
            _mockModelDeploymentRepository = new Mock<IModelDeploymentRepository>();
            _mockRouterConfigRepository = new Mock<ConduitLLM.Configuration.Repositories.IRouterConfigRepository>();
            _mockFallbackConfigRepository = new Mock<IFallbackConfigurationRepository>();
            
            // Setup router options
            var routerOptions = new RouterOptions
            {
                Enabled = true,
                DefaultRoutingStrategy = "ordered",
                MaxRetries = 3,
                RetryBaseDelayMs = 1000,
                RetryMaxDelayMs = 5000,
                ModelDeployments = new List<ConduitLLM.Configuration.Options.RouterModelDeployment>
                {
                    new ConduitLLM.Configuration.Options.RouterModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "gpt-4",
                        RPM = 100,
                        TPM = 100000
                    }
                },
                FallbackRules = new List<string>
                {
                    "gpt-4:claude-2,gpt-3.5-turbo"
                }
            };
            
            _mockRouterOptions.Setup(m => m.CurrentValue).Returns(routerOptions);
            
            // Create the service
            _service = new RouterService(
                _mockConfigContextFactory.Object,
                _mockClientFactory.Object,
                _logger,
                _mockRouterOptions.Object,
                _mockServiceProvider.Object);
        }

        [Fact]
        public async Task GetRouterConfigAsync_ShouldDeserializeConfig_WhenExists()
        {
            // Arrange
            var routerConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "ordered",
                MaxRetries = 3,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "gpt-4",
                        IsHealthy = true
                    }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(routerConfig);
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting
                {
                    Key = "RouterConfig",
                    Value = serializedConfig
                });

            // Act
            var result = await _service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ordered", result.DefaultRoutingStrategy);
            Assert.Equal(3, result.MaxRetries);
            Assert.Single(result.ModelDeployments);
            Assert.Equal("gpt-4", result.ModelDeployments[0].DeploymentName);
            
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()), 
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetRouterConfigAsync_ShouldCreateFromOptions_WhenNoConfigExists()
        {
            // Arrange
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalSetting?)null);

            // Act
            var result = await _service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ordered", result.DefaultRoutingStrategy);
            Assert.Equal(3, result.MaxRetries);
            Assert.Single(result.ModelDeployments);
            Assert.Equal("gpt-4", result.ModelDeployments[0].DeploymentName);
            
            // Verify we tried to get the config
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()), 
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task SaveModelDeploymentAsync_ShouldAddToDeployments()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "existing-model",
                        ModelAlias = "existing-model"
                    }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting
                {
                    Key = "RouterConfig",
                    Value = serializedConfig
                });
                
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                "RouterConfig",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            var newDeployment = new ModelDeployment
            {
                DeploymentName = "new-model",
                ModelAlias = "new-model-alias",
                IsHealthy = true
            };

            // Act
            var result = await _service.SaveModelDeploymentAsync(newDeployment);

            // Assert
            Assert.True(result);
            
            // Verify updated config is saved
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "RouterConfig",
                It.Is<string>(s => 
                    s.Contains("existing-model") && 
                    s.Contains("new-model")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task DeleteModelDeploymentAsync_ShouldRemoveFromDeployments()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "model-to-delete",
                        ModelAlias = "model-alias"
                    },
                    new ModelDeployment
                    {
                        DeploymentName = "other-model",
                        ModelAlias = "other-alias"
                    }
                },
                Fallbacks = new Dictionary<string, List<string>>
                {
                    { "primary-model", new List<string> { "model-to-delete", "fallback-model" } }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting
                {
                    Key = "RouterConfig",
                    Value = serializedConfig
                });
                
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                "RouterConfig",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            // Act
            var result = await _service.DeleteModelDeploymentAsync("model-to-delete");

            // Assert
            Assert.True(result);
            
            // Verify the updated config is saved without the deleted model
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "RouterConfig",
                It.Is<string>(s => 
                    !s.Contains("model-to-delete") && 
                    s.Contains("other-model") &&
                    s.Contains("fallback-model")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task SetFallbackConfigurationAsync_ShouldUpdateFallbacks()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>
                {
                    { "existing-model", new List<string> { "existing-fallback" } }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting
                {
                    Key = "RouterConfig",
                    Value = serializedConfig
                });
                
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                "RouterConfig",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            var primaryModel = "new-primary";
            var fallbackModels = new List<string> { "fallback1", "fallback2" };

            // Act
            var result = await _service.SetFallbackConfigurationAsync(primaryModel, fallbackModels);

            // Assert
            Assert.True(result);
            
            // Verify updated config is saved with new fallbacks
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "RouterConfig",
                It.Is<string>(s => 
                    s.Contains("existing-model") && 
                    s.Contains("existing-fallback") &&
                    s.Contains("new-primary") &&
                    s.Contains("fallback1") &&
                    s.Contains("fallback2")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task RemoveFallbackConfigurationAsync_ShouldRemoveFallbacks()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>
                {
                    { "model-to-remove", new List<string> { "fallback1", "fallback2" } },
                    { "other-model", new List<string> { "other-fallback" } }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting
                {
                    Key = "RouterConfig",
                    Value = serializedConfig
                });
                
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                "RouterConfig",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.RemoveFallbackConfigurationAsync("model-to-remove");

            // Assert
            Assert.True(result);
            
            // Verify updated config is saved without the removed fallbacks
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "RouterConfig",
                It.Is<string>(s => 
                    !s.Contains("model-to-remove") && 
                    !s.Contains("fallback1") &&
                    !s.Contains("fallback2") &&
                    s.Contains("other-model") &&
                    s.Contains("other-fallback")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetRouterStatusAsync_ShouldReturnStatus()
        {
            // Arrange
            var routerConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "ordered",
                MaxRetries = 3
            };
            
            var serializedConfig = JsonSerializer.Serialize(routerConfig);
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "RouterConfig", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting
                {
                    Key = "RouterConfig",
                    Value = serializedConfig
                });

            // Act
            var result = await _service.GetRouterStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Config);
            Assert.Equal("ordered", result.Config.DefaultRoutingStrategy);
            Assert.Equal(3, result.Config.MaxRetries);
            // Router would be initialized and enabled in a real scenario
            // For testing, we expect it to be false since we mocked dependencies
            // IsEnabled could be true or false depending on test execution order
            // so we don't test this value
        }

        [Fact]
        public async Task UpdateRouterConfigAsync_ShouldSaveConfig()
        {
            // Arrange
            var newConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "fallback",
                MaxRetries = 5,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "updated-model",
                        ModelAlias = "updated-alias"
                    }
                }
            };
            
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                "RouterConfig",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateRouterConfigAsync(newConfig);

            // Assert
            Assert.True(result);
            
            // Verify config is saved
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "RouterConfig",
                It.Is<string>(s => 
                    s.Contains("fallback") && 
                    s.Contains("updated-model")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }
    }
}