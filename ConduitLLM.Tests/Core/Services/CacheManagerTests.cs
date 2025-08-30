using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class CacheManagerTests : IDisposable
    {
        private readonly Mock<ILogger<CacheManager>> _loggerMock;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IDistributedCache> _distributedCacheMock;

        public CacheManagerTests()
        {
            _loggerMock = new Mock<ILogger<CacheManager>>();
            _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
            _distributedCacheMock = new Mock<IDistributedCache>();
        }

        [Fact]
        public async Task GetAsync_WhenValueInMemoryCache_ReturnsValueWithoutDistributedLookup()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const string value = "test-value";
            const CacheRegion region = CacheRegion.Default;

            await cacheManager.SetAsync(key, value, region);

            // Act
            var result = await cacheManager.GetAsync<string>(key, region);

            // Assert
            Assert.Equal(value, result);
            _distributedCacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task GetAsync_WhenValueNotInMemoryButInDistributed_ReturnsValueAndPopulatesMemory()
        {
            // Arrange
            var distributedValue = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes("distributed-value");
            
            // Mock needs to match the exact key format: "region:key"
            var expectedKey = $"{CacheRegion.ProviderHealth}:test-key";
            _distributedCacheMock.Setup(x => x.GetAsync(expectedKey, default))
                .ReturnsAsync(distributedValue);
                
            // Also setup the broader mock to catch any key variations
            _distributedCacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync(distributedValue);

            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const CacheRegion region = CacheRegion.ProviderHealth; // This region supports distributed cache

            // Debug: Check if distributed cache is enabled
            var config = cacheManager.GetRegionConfig(region);
            var debugMsg = $"UseDistributedCache: {config.UseDistributedCache}, DistCache!=null: {_distributedCacheMock.Object != null}";

            // Act
            var result = await cacheManager.GetAsync<string>(key, region);

            // Assert
            Assert.True(result == "distributed-value", $"Expected 'distributed-value', got '{result}'. Debug: {debugMsg}");
            _distributedCacheMock.Verify(x => x.GetAsync(expectedKey, default), Times.Once);

            // Verify memory cache was populated
            var memoryCachedValue = await cacheManager.GetAsync<string>(key, region);
            Assert.Equal("distributed-value", memoryCachedValue);
            _distributedCacheMock.Verify(x => x.GetAsync(expectedKey, default), Times.Once); // Still only once
        }

        [Fact]
        public async Task SetAsync_SetsInBothMemoryAndDistributedCache()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const string value = "test-value";
            const CacheRegion region = CacheRegion.VirtualKeys;

            // Act
            await cacheManager.SetAsync(key, value, region);

            // Assert
            var memoryResult = _memoryCache.Get<string>($"{region}:{key}");
            Assert.Equal(value, memoryResult);

            _distributedCacheMock.Verify(x => x.SetAsync(
                It.Is<string>(k => k == $"{region}:{key}"),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task GetOrCreateAsync_WhenCacheMiss_CallsFactoryAndCachesResult()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const string expectedValue = "factory-value";
            const CacheRegion region = CacheRegion.ModelMetadata;
            var factoryCalled = false;

            // Act
            var result = await cacheManager.GetOrCreateAsync(
                key,
                async () =>
                {
                    factoryCalled = true;
                    await Task.Delay(10); // Simulate async work
                    return expectedValue;
                },
                region);

            // Assert
            Assert.True(factoryCalled);
            Assert.Equal(expectedValue, result);

            // Verify value was cached
            var cachedValue = await cacheManager.GetAsync<string>(key, region);
            Assert.Equal(expectedValue, cachedValue);
        }

        [Fact]
        public async Task GetOrCreateAsync_WhenCacheHit_DoesNotCallFactory()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const string cachedValue = "cached-value";
            const CacheRegion region = CacheRegion.ModelMetadata;

            // Pre-populate cache
            await cacheManager.SetAsync(key, cachedValue, region);

            var factoryCalled = false;

            // Act
            var result = await cacheManager.GetOrCreateAsync<string>(
                key,
                async () =>
                {
                    factoryCalled = true;
                    await Task.Delay(10);
                    return "factory-value";
                },
                region);

            // Assert
            Assert.False(factoryCalled);
            Assert.Equal(cachedValue, result);
        }

        [Fact]
        public async Task RemoveAsync_RemovesFromBothCaches()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const string value = "test-value";
            const CacheRegion region = CacheRegion.RateLimits;

            // Pre-populate cache
            await cacheManager.SetAsync(key, value, region);

            // Act
            var removed = await cacheManager.RemoveAsync(key, region);

            // Assert
            Assert.True(removed);
            var result = await cacheManager.GetAsync<string>(key, region);
            Assert.Null(result);

            _distributedCacheMock.Verify(x => x.RemoveAsync(
                It.Is<string>(k => k == $"{region}:{key}"),
                default), Times.Once);
        }

        [Fact]
        public void RegionConfig_AppliesCorrectTTL()
        {
            // Arrange
            var options = new CacheManagerOptions
            {
                RegionConfigs = new Dictionary<CacheRegion, CacheRegionConfig>
                {
                    [CacheRegion.AuthTokens] = new CacheRegionConfig
                    {
                        Region = CacheRegion.AuthTokens,
                        DefaultTTL = TimeSpan.FromMinutes(10),
                        MaxTTL = TimeSpan.FromMinutes(15)
                    }
                }
            };

            var cacheManager = new CacheManager(
                _memoryCache,
                _distributedCacheMock.Object,
                _loggerMock.Object,
                Options.Create(options));

            // Act
            var config = cacheManager.GetRegionConfig(CacheRegion.AuthTokens);

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(10), config.DefaultTTL);
            Assert.Equal(TimeSpan.FromMinutes(15), config.MaxTTL);
        }

        [Fact]
        public async Task Statistics_TracksHitsAndMisses()
        {
            // Arrange
            // Setup distributed cache to return null for cache misses (to avoid exceptions)
            _distributedCacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync((byte[]?)null);
                
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const CacheRegion region = CacheRegion.ProviderHealth;

            // Act
            // Cache miss
            var missResult = await cacheManager.GetAsync<string>("missing-key", region);
            
            // Cache hit
            await cacheManager.SetAsync("existing-key", "value", region);
            var hitResult = await cacheManager.GetAsync<string>("existing-key", region);

            // Get statistics
            var stats = await cacheManager.GetRegionStatisticsAsync(region);

            // Assert
            Assert.Equal(1, stats.HitCount);
            Assert.Equal(1, stats.MissCount);
            Assert.Equal(0.5, stats.HitRate); // 50% hit rate
            Assert.Equal(1, stats.SetCount);
        }

        [Fact]
        public async Task HealthCheck_ReturnsHealthyStatus()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);

            // Act
            var health = await cacheManager.GetHealthStatusAsync();

            // Assert
            Assert.True(health.IsHealthy);
            Assert.True(health.ComponentStatus["MemoryCache"]);
            Assert.NotNull(health.MemoryCacheResponseTime);
            Assert.True(health.MemoryCacheResponseTime.Value.TotalMilliseconds >= 0);
        }

        [Fact]
        public async Task ClearRegionAsync_ClearsOnlySpecifiedRegion()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, null, _loggerMock.Object);
            
            // Add items to different regions
            await cacheManager.SetAsync("key1", "value1", CacheRegion.VirtualKeys);
            await cacheManager.SetAsync("key2", "value2", CacheRegion.RateLimits);

            // Act
            await cacheManager.ClearRegionAsync(CacheRegion.VirtualKeys);

            // Assert
            var virtualKeyValue = await cacheManager.GetAsync<string>("key1", CacheRegion.VirtualKeys);
            var rateLimitValue = await cacheManager.GetAsync<string>("key2", CacheRegion.RateLimits);

            Assert.Null(virtualKeyValue); // Should be cleared
            Assert.Equal("value2", rateLimitValue); // Should remain
        }

        [Fact]
        public async Task ExistsAsync_ReturnsTrueForExistingKey()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, null, _loggerMock.Object);
            const string key = "test-key";
            const CacheRegion region = CacheRegion.Default;

            await cacheManager.SetAsync(key, "value", region);

            // Act
            var exists = await cacheManager.ExistsAsync(key, region);
            var notExists = await cacheManager.ExistsAsync("non-existent", region);

            // Assert
            Assert.True(exists);
            Assert.False(notExists);
        }

        [Fact]
        public async Task RefreshAsync_UpdatesTTLWithoutChangingValue()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, _distributedCacheMock.Object, _loggerMock.Object);
            const string key = "test-key";
            const string value = "test-value";
            const CacheRegion region = CacheRegion.Default;

            await cacheManager.SetAsync(key, value, region, TimeSpan.FromMinutes(5));

            // Act
            var refreshed = await cacheManager.RefreshAsync(key, region, TimeSpan.FromMinutes(30));

            // Assert
            Assert.True(refreshed);
            var retrievedValue = await cacheManager.GetAsync<string>(key, region);
            Assert.Equal(value, retrievedValue);
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }

        // Performance benchmark test (simplified)
        [Fact]
        public async Task Performance_CacheOperationsHaveLowOverhead()
        {
            // Arrange
            var cacheManager = new CacheManager(_memoryCache, null, _loggerMock.Object);
            const int iterations = 1000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var key = $"perf-key-{i}";
                await cacheManager.SetAsync(key, i, CacheRegion.Default);
                var value = await cacheManager.GetAsync<int>(key, CacheRegion.Default);
                Assert.Equal(i, value);
            }

            stopwatch.Stop();

            // Assert
            var avgOperationTime = stopwatch.ElapsedMilliseconds / (double)(iterations * 2); // Set + Get
            Assert.True(avgOperationTime < 1.0, $"Average operation time {avgOperationTime}ms exceeds 1ms threshold");
        }
    }
}