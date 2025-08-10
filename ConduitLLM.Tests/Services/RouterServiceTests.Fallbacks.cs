using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Routing;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public partial class RouterServiceTests
    {
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