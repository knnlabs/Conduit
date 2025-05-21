using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class RouterServiceTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<ILogger<RouterServiceAdapter>> _mockLogger;
        private readonly RouterServiceAdapter _service;

        public RouterServiceTests()
        {
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockLogger = new Mock<ILogger<RouterServiceAdapter>>();
            _service = new RouterServiceAdapter(_mockAdminApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetRouterConfigAsync_ShouldReturnConfig()
        {
            // Arrange
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                FallbacksEnabled = true,
                MaxRetries = 3,
                RetryBaseDelayMs = 500,
                RetryMaxDelayMs = 10000,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        ModelName = "gpt-4",
                        ProviderName = "openai",
                        IsEnabled = true,
                        Priority = 1,
                        Weight = 1
                    }
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetRouterConfigAsync())
                .ReturnsAsync(config);
            
            // Act
            var result = await _service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("simple", result.DefaultRoutingStrategy);
            Assert.True(result.FallbacksEnabled);
            Assert.Equal(3, result.MaxRetries);
            Assert.Equal(500, result.RetryBaseDelayMs);
            Assert.Single(result.ModelDeployments);
            Assert.Equal("gpt-4", result.ModelDeployments[0].ModelName);
        }

        [Fact]
        public async Task GetModelDeploymentsAsync_ShouldReturnDeployments()
        {
            // Arrange
            var deployments = new List<ModelDeployment>
            {
                new ModelDeployment
                {
                    ModelName = "gpt-4",
                    ProviderName = "openai",
                    IsEnabled = true,
                    Priority = 1,
                    Weight = 1
                },
                new ModelDeployment
                {
                    ModelName = "claude-3-opus",
                    ProviderName = "anthropic",
                    IsEnabled = true,
                    Priority = 2,
                    Weight = 1
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetAllModelDeploymentsAsync())
                .ReturnsAsync(deployments);
            
            // Act
            var result = await _service.GetModelDeploymentsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("gpt-4", result[0].ModelName);
            Assert.Equal("claude-3-opus", result[1].ModelName);
        }

        [Fact]
        public async Task GetModelDeploymentAsync_ShouldReturnDeployment()
        {
            // Arrange
            var deployment = new ModelDeployment
            {
                ModelName = "gpt-4",
                ProviderName = "openai",
                IsEnabled = true,
                Priority = 1,
                Weight = 1
            };
            
            _mockAdminApiClient.Setup(api => api.GetModelDeploymentAsync("gpt-4"))
                .ReturnsAsync(deployment);
            
            // Act
            var result = await _service.GetModelDeploymentAsync("gpt-4");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("gpt-4", result.ModelName);
            Assert.Equal("openai", result.ProviderName);
        }

        [Fact]
        public async Task SaveModelDeploymentAsync_ShouldSaveDeployment()
        {
            // Arrange
            var deployment = new ModelDeployment
            {
                ModelName = "gpt-4",
                ProviderName = "openai",
                IsEnabled = true,
                Priority = 1,
                Weight = 1
            };
            
            _mockAdminApiClient.Setup(api => api.SaveModelDeploymentAsync(deployment))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.SaveModelDeploymentAsync(deployment);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.SaveModelDeploymentAsync(deployment), Times.Once);
        }

        [Fact]
        public async Task DeleteModelDeploymentAsync_ShouldDeleteDeployment()
        {
            // Arrange
            _mockAdminApiClient.Setup(api => api.DeleteModelDeploymentAsync("gpt-4"))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.DeleteModelDeploymentAsync("gpt-4");

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.DeleteModelDeploymentAsync("gpt-4"), Times.Once);
        }

        [Fact]
        public async Task GetFallbackConfigurationsAsync_ShouldReturnFallbacks()
        {
            // Arrange
            var fallbacks = new List<FallbackConfiguration>
            {
                new FallbackConfiguration
                {
                    PrimaryModelDeploymentId = "gpt-4",
                    FallbackModelDeploymentIds = new List<string> { "claude-3-opus", "gpt-3.5-turbo" }
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetAllFallbackConfigurationsAsync())
                .ReturnsAsync(fallbacks);
            
            // Act
            var result = await _service.GetFallbackConfigurationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(2, result["gpt-4"].Count);
            Assert.Contains("claude-3-opus", result["gpt-4"]);
            Assert.Contains("gpt-3.5-turbo", result["gpt-4"]);
        }

        [Fact]
        public async Task SetFallbackConfigurationAsync_ShouldSetFallback()
        {
            // Arrange
            var primaryModel = "gpt-4";
            var fallbackModels = new List<string> { "claude-3-opus", "gpt-3.5-turbo" };
            
            _mockAdminApiClient.Setup(api => api.SetFallbackConfigurationAsync(
                It.Is<FallbackConfiguration>(c => 
                    c.PrimaryModelDeploymentId == primaryModel && 
                    c.FallbackModelDeploymentIds.SequenceEqual(fallbackModels))))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.SetFallbackConfigurationAsync(primaryModel, fallbackModels);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.SetFallbackConfigurationAsync(
                It.Is<FallbackConfiguration>(c => 
                    c.PrimaryModelDeploymentId == primaryModel && 
                    c.FallbackModelDeploymentIds.SequenceEqual(fallbackModels))), 
                Times.Once);
        }

        [Fact]
        public async Task RemoveFallbackConfigurationAsync_ShouldRemoveFallback()
        {
            // Arrange
            var primaryModel = "gpt-4";
            
            _mockAdminApiClient.Setup(api => api.RemoveFallbackConfigurationAsync(primaryModel))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.RemoveFallbackConfigurationAsync(primaryModel);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.RemoveFallbackConfigurationAsync(primaryModel), Times.Once);
        }

        [Fact]
        public async Task UpdateRouterConfigAsync_ShouldUpdateConfig()
        {
            // Arrange
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                FallbacksEnabled = true,
                MaxRetries = 3,
                RetryBaseDelayMs = 500,
                RetryMaxDelayMs = 10000
            };
            
            _mockAdminApiClient.Setup(api => api.UpdateRouterConfigAsync(config))
                .ReturnsAsync(true);
            
            // Act
            var result = await _service.UpdateRouterConfigAsync(config);

            // Assert
            Assert.True(result);
            _mockAdminApiClient.Verify(api => api.UpdateRouterConfigAsync(config), Times.Once);
        }

        [Fact]
        public async Task GetRouterStatusAsync_ShouldReturnStatus()
        {
            // Arrange
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                FallbacksEnabled = true
            };
            
            _mockAdminApiClient.Setup(api => api.GetRouterConfigAsync())
                .ReturnsAsync(config);
            
            // Act
            var result = await _service.GetRouterStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(config, result.Config);
            Assert.True(result.IsEnabled);  // This is now using the extension method
        }
    }
}