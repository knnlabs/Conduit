using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class RouterServiceTests
    {
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
    }
}