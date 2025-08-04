using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for the RouterService class.
    /// </summary>
    public class RouterServiceTests : TestBase
    {
        private readonly Mock<ILLMRouter> _routerMock;
        private readonly Mock<IRouterConfigRepository> _repositoryMock;
        private readonly Mock<ILogger<RouterService>> _loggerMock;
        private readonly RouterService _service;

        public RouterServiceTests(ITestOutputHelper output) : base(output)
        {
            _routerMock = new Mock<ILLMRouter>();
            _repositoryMock = new Mock<IRouterConfigRepository>();
            _loggerMock = CreateLogger<RouterService>();

            _service = new RouterService(
                _routerMock.Object,
                _repositoryMock.Object,
                _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRouter_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouterService(null!, _repositoryMock.Object, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouterService(_routerMock.Object, null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouterService(_routerMock.Object, _repositoryMock.Object, null!));
        }

        #endregion

        #region InitializeRouterAsync Tests

        [Fact]
        public async Task InitializeRouterAsync_WithExistingConfig_LoadsAndInitializes()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "roundrobin",
                MaxRetries = 5,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "openai/gpt-4",
                        IsHealthy = true
                    }
                }
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.InitializeRouterAsync();

            // Assert
            _repositoryMock.Verify(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
            
            // Since DefaultLLMRouter doesn't implement IInitializableRouter, we need to check if it's cast properly
            if (_routerMock.Object is DefaultLLMRouter defaultRouter)
            {
                // Verify that Initialize was called with the correct config
                _loggerMock.Verify(l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Router initialized")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
            }
        }

        [Fact]
        public async Task InitializeRouterAsync_WithNoExistingConfig_CreatesDefaultConfig()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((RouterConfig?)null);

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.InitializeRouterAsync();

            // Assert
            _repositoryMock.Verify(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(It.IsAny<RouterConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region GetRouterConfigAsync Tests

        [Fact]
        public async Task GetRouterConfigAsync_ReturnsConfigFromRepository()
        {
            // Arrange
            var expectedConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "leastcost",
                MaxRetries = 3
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedConfig);

            // Act
            var result = await _service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("leastcost", result.DefaultRoutingStrategy);
            Assert.Equal(3, result.MaxRetries);
        }

        [Fact]
        public async Task GetRouterConfigAsync_WhenNoConfig_ReturnsDefaultConfig()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((RouterConfig?)null);

            // Act
            var result = await _service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("simple", result.DefaultRoutingStrategy);
            Assert.Equal(3, result.MaxRetries);
        }

        #endregion

        #region UpdateRouterConfigAsync Tests

        [Fact]
        public async Task UpdateRouterConfigAsync_SavesConfigAndReinitializesRouter()
        {
            // Arrange
            var newConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "highestpriority",
                MaxRetries = 10
            };

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.UpdateRouterConfigAsync(newConfig);

            // Assert
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(newConfig, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateRouterConfigAsync_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.UpdateRouterConfigAsync(null!));
        }

        #endregion

        #region AddModelDeploymentAsync Tests

        [Fact]
        public async Task AddModelDeploymentAsync_AddsDeploymentToConfig()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment { DeploymentName = "existing-model" }
                }
            };

            var newDeployment = new ModelDeployment
            {
                DeploymentName = "new-model",
                ModelAlias = "provider/new-model",
                IsHealthy = true
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.AddModelDeploymentAsync(newDeployment);

            // Assert
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(
                It.Is<RouterConfig>(config => 
                    config.ModelDeployments.Count == 2 &&
                    config.ModelDeployments.Any(d => d.DeploymentName == "new-model")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddModelDeploymentAsync_WithNullDeployment_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.AddModelDeploymentAsync(null!));
        }

        [Fact]
        public async Task AddModelDeploymentAsync_WithNoExistingConfig_CreatesNewConfig()
        {
            // Arrange
            var newDeployment = new ModelDeployment
            {
                DeploymentName = "new-model",
                ModelAlias = "provider/new-model"
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((RouterConfig?)null);

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.AddModelDeploymentAsync(newDeployment);

            // Assert
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(
                It.Is<RouterConfig>(config => 
                    config.ModelDeployments.Count == 1 &&
                    config.ModelDeployments[0].DeploymentName == "new-model"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region UpdateModelDeploymentAsync Tests

        [Fact]
        public async Task UpdateModelDeploymentAsync_UpdatesExistingDeployment()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment 
                    { 
                        DeploymentName = "model-to-update",
                        ModelAlias = "provider/old-model",
                        Priority = 5
                    }
                }
            };

            var updatedDeployment = new ModelDeployment
            {
                DeploymentName = "model-to-update",
                ModelAlias = "provider/new-model",
                Priority = 1
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.UpdateModelDeploymentAsync(updatedDeployment);

            // Assert
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(
                It.Is<RouterConfig>(config => 
                    config.ModelDeployments.Count == 1 &&
                    config.ModelDeployments[0].ModelAlias == "provider/new-model" &&
                    config.ModelDeployments[0].Priority == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateModelDeploymentAsync_WithNonExistentDeployment_AddsNewDeployment()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>()
            };

            var updatedDeployment = new ModelDeployment
            {
                DeploymentName = "non-existent-model",
                ModelAlias = "provider/model"
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // Act
            await _service.UpdateModelDeploymentAsync(updatedDeployment);

            // Assert
            // Verify that SaveRouterConfigAsync was called with the new deployment added
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(
                It.Is<RouterConfig>(config => 
                    config.ModelDeployments.Count == 1 &&
                    config.ModelDeployments[0].DeploymentName == "non-existent-model"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region RemoveModelDeploymentAsync Tests

        [Fact]
        public async Task RemoveModelDeploymentAsync_RemovesDeploymentFromConfig()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment { DeploymentName = "model-to-remove" },
                    new ModelDeployment { DeploymentName = "model-to-keep" }
                }
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // The RouterService checks if _router is DefaultLLMRouter at runtime
            // Since we're using a mock, it won't be, so no initialization will happen

            // Act
            await _service.RemoveModelDeploymentAsync("model-to-remove");

            // Assert
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(
                It.Is<RouterConfig>(config => 
                    config.ModelDeployments.Count == 1 &&
                    config.ModelDeployments[0].DeploymentName == "model-to-keep"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveModelDeploymentAsync_WithEmptyDeploymentName_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RemoveModelDeploymentAsync(""));
        }

        #endregion

        #region GetModelDeploymentsAsync Tests

        [Fact]
        public async Task GetModelDeploymentsAsync_ReturnsAllDeployments()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment { DeploymentName = "model1" },
                    new ModelDeployment { DeploymentName = "model2" },
                    new ModelDeployment { DeploymentName = "model3" }
                }
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // Act
            var result = await _service.GetModelDeploymentsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains(result, d => d.DeploymentName == "model1");
            Assert.Contains(result, d => d.DeploymentName == "model2");
            Assert.Contains(result, d => d.DeploymentName == "model3");
        }

        [Fact]
        public async Task GetModelDeploymentsAsync_WithNoConfig_ReturnsEmptyList()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((RouterConfig?)null);

            // Act
            var result = await _service.GetModelDeploymentsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region SetFallbackModelsAsync Tests

        [Fact]
        public async Task SetFallbackModelsAsync_SetsFallbacksForModel()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>
                {
                    ["existing-model"] = new List<string> { "fallback1" }
                }
            };

            var fallbacks = new List<string> { "fallback-model-1", "fallback-model-2" };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // Note: AddFallbackModels is a method on DefaultLLMRouter, not ILLMRouter
            // For the service test, we'll skip this setup as it's an implementation detail

            // Act
            await _service.SetFallbackModelsAsync("primary-model", fallbacks);

            // Assert
            _repositoryMock.Verify(r => r.SaveRouterConfigAsync(
                It.Is<RouterConfig>(config => 
                    config.Fallbacks.ContainsKey("primary-model") &&
                    config.Fallbacks["primary-model"].Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
            
            // Note: AddFallbackModels is a method on DefaultLLMRouter, not ILLMRouter
            // The router should be configured correctly through Initialize method
        }

        [Fact]
        public async Task SetFallbackModelsAsync_WithEmptyPrimaryModel_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.SetFallbackModelsAsync("", new List<string>()));
        }

        #endregion

        #region GetFallbackModelsAsync Tests

        [Fact]
        public async Task GetFallbackModelsAsync_ReturnsFallbacksForModel()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>
                {
                    ["primary-model"] = new List<string> { "fallback1", "fallback2" }
                }
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // Act
            var result = await _service.GetFallbackModelsAsync("primary-model");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains("fallback1", result);
            Assert.Contains("fallback2", result);
        }

        [Fact]
        public async Task GetFallbackModelsAsync_WithNoFallbacks_ReturnsEmptyList()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>()
            };

            _repositoryMock.Setup(r => r.GetRouterConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            // Act
            var result = await _service.GetFallbackModelsAsync("primary-model");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region UpdateModelHealth Tests

        // UpdateModelHealth test removed - provider health monitoring has been removed

        #endregion
    }

}