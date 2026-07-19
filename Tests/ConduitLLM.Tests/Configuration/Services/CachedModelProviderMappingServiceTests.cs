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

            _testMapping = CreateFullyLoadedMapping();
        }

        /// <summary>
        /// Creates a mapping with the full navigation graph the repository always loads
        /// (Provider, ModelProviderTypeAssociation, and Model).
        /// </summary>
        private static ModelProviderMapping CreateFullyLoadedMapping()
        {
            return new ModelProviderMapping
            {
                Id = TestMappingId,
                ModelAlias = TestModelAlias,
                ProviderId = 1,
                ProviderModelId = "gpt-4-0613",
                IsEnabled = true,
                Provider = new Provider
                {
                    Id = 1,
                    ProviderName = "OpenAI",
                    ProviderType = ProviderType.OpenAI
                },
                ModelProviderTypeAssociationId = 10,
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation
                {
                    Id = 10,
                    ModelId = 20,
                    Identifier = "gpt-4-0613",
                    Model = new Model
                    {
                        Id = 20,
                        Name = "GPT-4",
                        SupportsImageGeneration = true
                    }
                }
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

            // Verify cache invalidation: by-id, by-alias, all-by-alias (failover list), all-mappings
            _mockCacheManager.Verify(x => x.RemoveManyAsync(
                It.Is<IEnumerable<string>>(keys => keys.Count() == 4),
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

        #region Incomplete Cache Entry Fallback Tests (issue #959)

        /// <summary>
        /// Simulates what the distributed cache tier does to a mapping: a System.Text.Json
        /// round-trip, which drops [JsonIgnore] navigation properties like
        /// ModelProviderTypeAssociation.Model (and its capability flags with it).
        /// </summary>
        private static ModelProviderMapping RoundTripThroughJson(ModelProviderMapping mapping)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(mapping);
            return System.Text.Json.JsonSerializer.Deserialize<ModelProviderMapping>(json)!;
        }

        [Fact]
        public void JsonRoundTrip_DropsModelNavigationProperty()
        {
            // Documents the root cause of issue #959: the distributed cache serializes entities
            // with System.Text.Json, and [JsonIgnore] strips the Model graph.
            var lossy = RoundTripThroughJson(CreateFullyLoadedMapping());

            Assert.NotNull(lossy.ModelProviderTypeAssociation);
            Assert.Null(lossy.ModelProviderTypeAssociation.Model);
        }

        [Fact]
        public async Task GetMappingByModelAliasAsync_CachedEntryMissingModelGraph_ReloadsFromDatabaseAndRecaches()
        {
            // Arrange
            var cacheKey = $"model:mapping:{TestModelAlias}";
            var lossyMapping = RoundTripThroughJson(_testMapping);

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(lossyMapping);

            _mockInnerService
                .Setup(x => x.GetMappingByModelAliasAsync(TestModelAlias))
                .ReturnsAsync(_testMapping);

            // Act
            var result = await _cachedService.GetMappingByModelAliasAsync(TestModelAlias);

            // Assert - the incomplete cached entry is bypassed and the full mapping returned
            Assert.NotNull(result);
            Assert.NotNull(result.ModelProviderTypeAssociation?.Model);
            Assert.True(result.ModelProviderTypeAssociation.Model.SupportsImageGeneration);

            _mockInnerService.Verify(x => x.GetMappingByModelAliasAsync(TestModelAlias), Times.Once);

            // The full mapping is re-cached (repopulating the memory tier)
            _mockCacheManager.Verify(x => x.SetAsync(
                cacheKey,
                _testMapping,
                CacheRegion.ModelMetadata,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMappingByModelAliasAsync_CachedEntryMissingModelGraph_MappingDeleted_RemovesStaleEntry()
        {
            // Arrange
            var cacheKey = $"model:mapping:{TestModelAlias}";
            var lossyMapping = RoundTripThroughJson(_testMapping);

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(lossyMapping);

            _mockInnerService
                .Setup(x => x.GetMappingByModelAliasAsync(TestModelAlias))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act
            var result = await _cachedService.GetMappingByModelAliasAsync(TestModelAlias);

            // Assert
            Assert.Null(result);

            _mockCacheManager.Verify(x => x.RemoveAsync(
                cacheKey,
                CacheRegion.ModelMetadata,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMappingByIdAsync_CachedEntryMissingModelGraph_ReloadsFromDatabaseAndRecaches()
        {
            // Arrange
            var cacheKey = $"model:mapping:id:{TestMappingId}";
            var lossyMapping = RoundTripThroughJson(_testMapping);

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<Task<ModelProviderMapping?>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(lossyMapping);

            _mockInnerService
                .Setup(x => x.GetMappingByIdAsync(TestMappingId))
                .ReturnsAsync(_testMapping);

            // Act
            var result = await _cachedService.GetMappingByIdAsync(TestMappingId);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ModelProviderTypeAssociation?.Model);

            _mockInnerService.Verify(x => x.GetMappingByIdAsync(TestMappingId), Times.Once);
            _mockCacheManager.Verify(x => x.SetAsync(
                cacheKey,
                _testMapping,
                CacheRegion.ModelMetadata,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllMappingsAsync_CachedEntryMissingModelGraph_ReloadsFromDatabaseAndRecaches()
        {
            // Arrange
            var lossyMappings = new List<ModelProviderMapping> { RoundTripThroughJson(_testMapping) };
            var freshMappings = new List<ModelProviderMapping> { _testMapping };

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    "model:mapping:all",
                    It.IsAny<Func<Task<List<ModelProviderMapping>>>>(),
                    CacheRegion.ModelMetadata,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(lossyMappings);

            _mockInnerService
                .Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(freshMappings);

            // Act
            var result = await _cachedService.GetAllMappingsAsync();

            // Assert
            Assert.Single(result);
            Assert.NotNull(result[0].ModelProviderTypeAssociation?.Model);

            _mockInnerService.Verify(x => x.GetAllMappingsAsync(), Times.Once);
            _mockCacheManager.Verify(x => x.SetAsync(
                "model:mapping:all",
                freshMappings,
                CacheRegion.ModelMetadata,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMappingByModelAliasAsync_CachedEntryComplete_DoesNotHitDatabase()
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
                .ReturnsAsync(_testMapping);

            // Act
            var result = await _cachedService.GetMappingByModelAliasAsync(TestModelAlias);

            // Assert
            Assert.NotNull(result);
            _mockInnerService.Verify(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()), Times.Never);
            _mockCacheManager.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<ModelProviderMapping>(),
                It.IsAny<CacheRegion>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Never);
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
