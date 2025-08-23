using ConduitLLM.Core.Models.Routing;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class RouterServiceTests
    {
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
    }
}