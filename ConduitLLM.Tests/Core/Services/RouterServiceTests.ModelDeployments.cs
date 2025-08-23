using ConduitLLM.Core.Models.Routing;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class RouterServiceTests
    {
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
    }
}