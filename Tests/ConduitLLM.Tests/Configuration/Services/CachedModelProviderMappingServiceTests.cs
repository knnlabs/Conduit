using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Services
{
    /// <summary>
    /// Unit tests for CachedModelProviderMappingService to verify caching behavior
    /// </summary>
    public class CachedModelProviderMappingServiceTests
    {
        private readonly Mock<IModelProviderMappingService> _mockInnerService;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<ILogger<CachedModelProviderMappingService>> _mockLogger;
        private readonly CachedModelProviderMappingService _cachedService;

        private const string TestModelAlias = "gpt-4";
        private const int TestMappingId = 123;
        private readonly ModelProviderMapping _testMapping;

        public CachedModelProviderMappingServiceTests()
        {
            _mockInnerService = new Mock<IModelProviderMappingService>();
            _mockCacheManager = new Mock<ICacheManager>();
            _mockLogger = new Mock<ILogger<CachedModelProviderMappingService>>();

            _cachedService = new CachedModelProviderMappingService(
                _mockInnerService.Object,
                _mockCacheManager.Object,
                _mockLogger.Object);

            _testMapping = new ModelProviderMapping
            {
                Id = TestMappingId,
                ModelAlias = TestModelAlias,
                ProviderId = 1,
                ProviderModelId = "gpt-4-0613",
                IsEnabled = true
            };
        }

        #region GetMappingByModelAliasAsync Tests

        [Fact]
        public async Task GetMappingByModelAliasAsync_CacheHit_ReturnsFromCache()
        {
            // Arrange
            var cacheKey = $"model:mapping:{TestModelAlias}";
            var expectedMapping = _testMapping;

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedMapping);

            // Act
            var result = await _cachedService.GetMappingByModelAliasAsync(TestModelAlias);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestModelAlias, result.ModelAlias);
            Assert.Equal(TestMappingId, result.Id);

            // Verify cache was checked
            _mockCacheManager.Verify(x => x.GetOrCreateAsync(
                cacheKey,
                It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                CacheRegion.ModelMetadata,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMappingByModelAliasAsync_NullAlias_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _cachedService.GetMappingByModelAliasAsync(null!));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _cachedService.GetMappingByModelAliasAsync(string.Empty));
        }

        [Fact]
        public async Task GetMappingByModelAliasAsync_CacheFails_FallsBackToDatabase()
        {
            // Arrange
            var cacheKey = $"model:mapping:{TestModelAlias}";

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Cache failure"));

            _mockInnerService
                .Setup(x => x.GetMappingByModelAliasAsync(TestModelAlias))
                .ReturnsAsync(_testMapping);

            // Act
            var result = await _cachedService.GetMappingByModelAliasAsync(TestModelAlias);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestModelAlias, result.ModelAlias);

            // Verify fallback to database
            _mockInnerService.Verify(x => x.GetMappingByModelAliasAsync(TestModelAlias), Times.Once);
        }

        #endregion

        #region GetMappingByIdAsync Tests

        [Fact]
        public async Task GetMappingByIdAsync_CacheHit_ReturnsFromCache()
        {
            // Arrange
            var cacheKey = $"model:mapping:id:{TestMappingId}";

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_testMapping);

            // Act
            var result = await _cachedService.GetMappingByIdAsync(TestMappingId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestMappingId, result.Id);

            _mockCacheManager.Verify(x => x.GetOrCreateAsync(
                cacheKey,
                It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                CacheRegion.ModelMetadata,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region AddMappingAsync Tests

        [Fact]
        public async Task AddMappingAsync_InvalidatesCache()
        {
            // Arrange
            var mapping = _testMapping;

            _mockInnerService
                .Setup(x => x.AddMappingAsync(mapping))
                .Returns(Task.CompletedTask);

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, default))
                .ReturnsAsync(3); // 3 keys invalidated

            // Act
            await _cachedService.AddMappingAsync(mapping);

            // Assert
            _mockInnerService.Verify(x => x.AddMappingAsync(mapping), Times.Once);

            // Verify cache invalidation
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.Is<IEnumerable<string>>(keys =>
                    keys.Contains($"model:mapping:{TestModelAlias}") &&
                    keys.Contains($"model:mapping:id:{TestMappingId}") &&
                    keys.Contains("model:mapping:all")),
                CacheRegion.ModelMetadata,
                default), Times.Once);
        }

        [Fact]
        public async Task AddMappingAsync_NullMapping_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cachedService.AddMappingAsync(null!));
        }

        #endregion

        #region UpdateMappingAsync Tests

        [Fact]
        public async Task UpdateMappingAsync_InvalidatesCache()
        {
            // Arrange
            var mapping = _testMapping;

            _mockInnerService
                .Setup(x => x.UpdateMappingAsync(mapping))
                .Returns(Task.CompletedTask);

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, default))
                .ReturnsAsync(3);

            // Act
            await _cachedService.UpdateMappingAsync(mapping);

            // Assert
            _mockInnerService.Verify(x => x.UpdateMappingAsync(mapping), Times.Once);

            // Verify cache invalidation
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.Is<IEnumerable<string>>(keys => keys.Count() == 3),
                CacheRegion.ModelMetadata,
                default), Times.Once);
        }

        #endregion

        #region DeleteMappingAsync Tests

        [Fact]
        public async Task DeleteMappingAsync_InvalidatesCache()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.GetMappingByIdAsync(TestMappingId))
                .ReturnsAsync(_testMapping);

            _mockInnerService
                .Setup(x => x.DeleteMappingAsync(TestMappingId))
                .Returns(Task.CompletedTask);

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, default))
                .ReturnsAsync(3);

            // Act
            await _cachedService.DeleteMappingAsync(TestMappingId);

            // Assert
            _mockInnerService.Verify(x => x.GetMappingByIdAsync(TestMappingId), Times.Once);
            _mockInnerService.Verify(x => x.DeleteMappingAsync(TestMappingId), Times.Once);

            // Verify cache invalidation
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.IsAny<IEnumerable<string>>(),
                CacheRegion.ModelMetadata,
                default), Times.Once);
        }

        [Fact]
        public async Task DeleteMappingAsync_MappingNotFound_StillInvalidatesById()
        {
            // Arrange
            _mockInnerService
                .Setup(x => x.GetMappingByIdAsync(TestMappingId))
                .ReturnsAsync((ModelProviderMapping?)null);

            _mockInnerService
                .Setup(x => x.DeleteMappingAsync(TestMappingId))
                .Returns(Task.CompletedTask);

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, default))
                .ReturnsAsync(2); // Only ID and all mappings, no alias

            // Act
            await _cachedService.DeleteMappingAsync(TestMappingId);

            // Assert
            _mockInnerService.Verify(x => x.DeleteMappingAsync(TestMappingId), Times.Once);

            // Should still invalidate cache even if mapping not found
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.IsAny<IEnumerable<string>>(),
                CacheRegion.ModelMetadata,
                default), Times.Once);
        }

        #endregion

        #region ValidateAndCreateMappingAsync Tests

        [Fact]
        public async Task ValidateAndCreateMappingAsync_Success_InvalidatesCache()
        {
            // Arrange
            var mapping = _testMapping;
            var expectedResult = (true, (string?)null, mapping);

            _mockInnerService
                .Setup(x => x.ValidateAndCreateMappingAsync(mapping))
                .ReturnsAsync(expectedResult);

            _mockCacheManager
                .Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), CacheRegion.ModelMetadata, default))
                .ReturnsAsync(3);

            // Act
            var result = await _cachedService.ValidateAndCreateMappingAsync(mapping);

            // Assert
            Assert.True(result.success);
            Assert.Null(result.errorMessage);
            Assert.NotNull(result.createdMapping);

            // Verify cache invalidation
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.IsAny<IEnumerable<string>>(),
                CacheRegion.ModelMetadata,
                default), Times.Once);
        }

        [Fact]
        public async Task ValidateAndCreateMappingAsync_Failure_DoesNotInvalidateCache()
        {
            // Arrange
            var mapping = _testMapping;
            var expectedResult = (false, "Validation failed", (ModelProviderMapping?)null);

            _mockInnerService
                .Setup(x => x.ValidateAndCreateMappingAsync(mapping))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _cachedService.ValidateAndCreateMappingAsync(mapping);

            // Assert
            Assert.False(result.success);
            Assert.Equal("Validation failed", result.errorMessage);

            // Should NOT invalidate cache on failure
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CacheRegion>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region GetAllMappingsAsync Tests

        [Fact]
        public async Task GetAllMappingsAsync_CachesResult()
        {
            // Arrange
            var expectedMappings = new List<ModelProviderMapping> { _testMapping };

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    "model:mapping:all",
                    It.IsAny<Func<Task<List<ModelProviderMapping>>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedMappings);

            // Act
            var result = await _cachedService.GetAllMappingsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(TestModelAlias, result[0].ModelAlias);

            _mockCacheManager.Verify(x => x.GetOrCreateAsync(
                "model:mapping:all",
                It.IsAny<Func<Task<List<ModelProviderMapping>>>>(),
                CacheRegion.ModelMetadata,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Pass-through Methods Tests

        [Fact]
        public async Task ProviderExistsByIdAsync_PassesThrough()
        {
            // Arrange
            const int providerId = 42;
            _mockInnerService
                .Setup(x => x.ProviderExistsByIdAsync(providerId))
                .ReturnsAsync(true);

            // Act
            var result = await _cachedService.ProviderExistsByIdAsync(providerId);

            // Assert
            Assert.True(result);
            _mockInnerService.Verify(x => x.ProviderExistsByIdAsync(providerId), Times.Once);

            // Should not interact with cache for this method
            _mockCacheManager.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAvailableProvidersAsync_PassesThrough()
        {
            // Arrange
            var expectedProviders = new List<(int Id, string ProviderName)>
            {
                (1, "OpenAI"),
                (2, "Anthropic")
            };

            _mockInnerService
                .Setup(x => x.GetAvailableProvidersAsync())
                .ReturnsAsync(expectedProviders);

            // Act
            var result = await _cachedService.GetAvailableProvidersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            _mockInnerService.Verify(x => x.GetAvailableProvidersAsync(), Times.Once);

            // Should not interact with cache for this method
            _mockCacheManager.VerifyNoOtherCalls();
        }

        #endregion
    }
}
