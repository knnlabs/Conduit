using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for CachedModelCostService to verify caching decorator behavior
    /// </summary>
    public class CachedModelCostServiceTests
    {
        private readonly Mock<IModelCostService> _mockInnerService;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<ILogger<CachedModelCostService>> _mockLogger;
        private readonly CachedModelCostService _cachedService;

        private const string TestModelId = "gpt-4";
        private const int TestModelCostId = 42;

        private readonly ModelCost _testModelCost;

        public CachedModelCostServiceTests()
        {
            _mockInnerService = new Mock<IModelCostService>();
            _mockCacheManager = new Mock<ICacheManager>();
            _mockLogger = new Mock<ILogger<CachedModelCostService>>();

            _cachedService = new CachedModelCostService(
                _mockInnerService.Object,
                _mockCacheManager.Object,
                _mockLogger.Object);

            _testModelCost = new ModelCost
            {
                Id = TestModelCostId,
                CostName = "GPT-4 Cost",
                InputCostPerMillionTokens = 30m,
                OutputCostPerMillionTokens = 60m,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };
        }

        #region GetCostForModelAsync Tests

        [Fact]
        public async Task GetCostForModelAsync_CacheHit_ReturnsFromCache()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.GetAsync<ModelCost>(
                    It.IsAny<string>(),
                    CacheRegion.ModelCosts,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_testModelCost);

            // Act
            var result = await _cachedService.GetCostForModelAsync(TestModelId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestModelCostId, result.Id);

            // Should NOT call inner service
            _mockInnerService.Verify(x => x.GetCostForModelAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCostForModelAsync_CacheMiss_FallsBackToDatabase()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.GetAsync<ModelCost>(
                    It.IsAny<string>(),
                    CacheRegion.ModelCosts,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            _mockInnerService
                .Setup(x => x.GetCostForModelAsync(TestModelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_testModelCost);

            // Act
            var result = await _cachedService.GetCostForModelAsync(TestModelId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestModelCostId, result.Id);

            // Should call inner service
            _mockInnerService.Verify(x => x.GetCostForModelAsync(
                TestModelId, It.IsAny<CancellationToken>()), Times.Once);

            // Should cache the result
            _mockCacheManager.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                _testModelCost,
                CacheRegion.ModelCosts,
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetCostForModelAsync_CacheError_FallsBackToDatabase()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.GetAsync<ModelCost>(
                    It.IsAny<string>(),
                    CacheRegion.ModelCosts,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Cache failure"));

            _mockInnerService
                .Setup(x => x.GetCostForModelAsync(TestModelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_testModelCost);

            // Act
            var result = await _cachedService.GetCostForModelAsync(TestModelId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestModelCostId, result.Id);

            // Should fall back to inner service
            _mockInnerService.Verify(x => x.GetCostForModelAsync(
                TestModelId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetCostForModelAsync_EmptyModelId_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _cachedService.GetCostForModelAsync(""));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _cachedService.GetCostForModelAsync("  "));
        }

        #endregion

        #region GetCostByIdAsync Tests

        [Fact]
        public async Task GetCostByIdAsync_CacheHit_ReturnsFromCache()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.GetAsync<ModelCost>(
                    It.IsAny<string>(),
                    CacheRegion.ModelCosts,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_testModelCost);

            // Act
            var result = await _cachedService.GetCostByIdAsync(TestModelCostId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestModelCostId, result.Id);

            _mockInnerService.Verify(x => x.GetCostByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCostByIdAsync_CacheMiss_QueriesDatabase()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.GetAsync<ModelCost>(
                    It.IsAny<string>(),
                    CacheRegion.ModelCosts,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            _mockInnerService
                .Setup(x => x.GetCostByIdAsync(TestModelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_testModelCost);

            // Act
            var result = await _cachedService.GetCostByIdAsync(TestModelCostId);

            // Assert
            Assert.NotNull(result);
            _mockInnerService.Verify(x => x.GetCostByIdAsync(
                TestModelCostId, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ListModelCostsAsync Tests

        [Fact]
        public async Task ListModelCostsAsync_CacheHit_ReturnsFromCache()
        {
            // Arrange
            var costs = new List<ModelCost> { _testModelCost };

            _mockCacheManager
                .Setup(x => x.GetAsync<List<ModelCost>>(
                    "modelcost:all",
                    CacheRegion.ModelCosts,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(costs);

            // Act
            var result = await _cachedService.ListModelCostsAsync();

            // Assert
            Assert.Single(result);
            _mockInnerService.Verify(x => x.ListModelCostsAsync(
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Write Operation Tests

        [Fact]
        public async Task AddModelCostAsync_InvalidatesCache()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.AddModelCostAsync(_testModelCost, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _cachedService.AddModelCostAsync(_testModelCost);

            // Assert
            _mockInnerService.Verify(x => x.AddModelCostAsync(
                _testModelCost, It.IsAny<CancellationToken>()), Times.Once);

            _mockCacheManager.Verify(x => x.ClearRegionAsync(
                CacheRegion.ModelCosts, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_Success_InvalidatesCache()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.UpdateModelCostAsync(_testModelCost, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _cachedService.UpdateModelCostAsync(_testModelCost);

            // Assert
            Assert.True(result);
            _mockCacheManager.Verify(x => x.ClearRegionAsync(
                CacheRegion.ModelCosts, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_NotFound_DoesNotInvalidateCache()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.UpdateModelCostAsync(_testModelCost, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _cachedService.UpdateModelCostAsync(_testModelCost);

            // Assert
            Assert.False(result);
            _mockCacheManager.Verify(x => x.ClearRegionAsync(
                It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteModelCostAsync_Success_InvalidatesCache()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.DeleteModelCostAsync(TestModelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _cachedService.DeleteModelCostAsync(TestModelCostId);

            // Assert
            Assert.True(result);
            _mockCacheManager.Verify(x => x.ClearRegionAsync(
                CacheRegion.ModelCosts, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteModelCostAsync_NotFound_DoesNotInvalidateCache()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.DeleteModelCostAsync(TestModelCostId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _cachedService.DeleteModelCostAsync(TestModelCostId);

            // Assert
            Assert.False(result);
            _mockCacheManager.Verify(x => x.ClearRegionAsync(
                It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region ClearCacheAsync Tests

        [Fact]
        public async Task ClearCacheAsync_ClearsRegion()
        {
            // Act
            await _cachedService.ClearCacheAsync();

            // Assert
            _mockCacheManager.Verify(x => x.ClearRegionAsync(
                CacheRegion.ModelCosts, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ClearCacheAsync_CacheError_DoesNotThrow()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.ClearRegionAsync(CacheRegion.ModelCosts, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Cache failure"));

            // Act & Assert — should not throw
            await _cachedService.ClearCacheAsync();
        }

        #endregion
    }
}
