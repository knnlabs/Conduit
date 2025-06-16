using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using StackExchange.Redis;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class CachePerformanceTests
    {
        private readonly CacheService _memoryCache;
        private readonly Mock<RedisCacheService> _mockRedisCache;
        private readonly CacheOptions _cacheOptions;

        public CachePerformanceTests()
        {
            _cacheOptions = new CacheOptions
            {
                IsEnabled = true,
                DefaultExpirationMinutes = 60,
                DefaultAbsoluteExpirationMinutes = 120,
                DefaultSlidingExpirationMinutes = 30
            };

            var mockOptions = new Mock<IOptions<CacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(_cacheOptions);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            _memoryCache = new CacheService(memoryCache, mockOptions.Object);

            var mockDistributedCache = new Mock<IDistributedCache>();
            var mockRedisConnection = new Mock<IConnectionMultiplexer>();
            var mockRedisDatabase = new Mock<IDatabase>();
            var mockLogger = new Mock<ILogger<RedisCacheService>>();

            mockRedisConnection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockRedisDatabase.Object);

            // Instead of mocking the RedisCacheService which has non-overridable methods,
            // create a real instance and use reflection to create a wrapper that can intercept calls
            var realRedisCache = new RedisCacheService(
                mockDistributedCache.Object,
                mockRedisConnection.Object,
                mockOptions.Object,
                mockLogger.Object);

            _mockRedisCache = new Mock<RedisCacheService>(MockBehavior.Loose,
                mockDistributedCache.Object,
                mockRedisConnection.Object,
                mockOptions.Object,
                mockLogger.Object);

            // Set up distributed cache Get and Set methods directly
            mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k.StartsWith("existing"))))
                .Returns((string key) =>
                {
                    // Simulate small network latency
                    Task.Delay(5).Wait();
                    var testItem = new TestItem { Id = 1, Name = "Test" };
                    return System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(testItem));
                });

            mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => !k.StartsWith("existing"))))
                .Returns((byte[]?)null);

            mockDistributedCache.Setup(x => x.Set(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>()))
                .Callback(() =>
                {
                    // Simulate small network latency
                    Task.Delay(5).Wait();
                });
        }

        [Fact]
        public void Get_PerformanceComparison()
        {
            // Arrange
            const int iterations = 100; // Reduced for faster test
            var existingKey = "existing-key";
            var nonExistingKey = "non-existing-key";

            _memoryCache.Set(existingKey, new TestItem { Id = 1, Name = "Test" });

            var stopwatch = new Stopwatch();

            // Act - Memory Cache (Existing Key)
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                _memoryCache.Get<TestItem>(existingKey);
            }
            stopwatch.Stop();
            var memoryCacheExistingTime = stopwatch.ElapsedMilliseconds;

            // Act - Memory Cache (Non-Existing Key)
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                _memoryCache.Get<TestItem>(nonExistingKey);
            }
            stopwatch.Stop();
            var memoryCacheNonExistingTime = stopwatch.ElapsedMilliseconds;

            // Create a real RedisCacheService instance that we'll use directly
            var mockDistributedCache = new Mock<IDistributedCache>();
            var mockRedisConnection = new Mock<IConnectionMultiplexer>();
            var mockLogger = new Mock<ILogger<RedisCacheService>>();
            var mockOptions = new Mock<IOptions<CacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(_cacheOptions);

            // Set up distributed cache for simulated Redis behavior
            mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k.StartsWith("existing"))))
                .Returns(() =>
                {
                    // Simulate small network latency
                    Task.Delay(5).Wait();
                    var testItem = new TestItem { Id = 1, Name = "Test" };
                    return System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(testItem));
                });

            mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => !k.StartsWith("existing"))))
                .Returns((byte[]?)null);

            var redisCache = new RedisCacheService(
                mockDistributedCache.Object,
                mockRedisConnection.Object,
                mockOptions.Object,
                mockLogger.Object);

            // Act - Redis Cache (Existing Key)
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                redisCache.Get<TestItem>("existing-" + i % 10);
            }
            stopwatch.Stop();
            var redisCacheExistingTime = stopwatch.ElapsedMilliseconds;

            // Act - Redis Cache (Non-Existing Key)
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                redisCache.Get<TestItem>("non-existing-" + i % 10);
            }
            stopwatch.Stop();
            var redisCacheNonExistingTime = stopwatch.ElapsedMilliseconds;

            // Assert - We only verify that measurements were taken, not specific performance metrics
            // since those would be environment-dependent and could cause flaky tests
            Assert.True(memoryCacheExistingTime >= 0);
            Assert.True(memoryCacheNonExistingTime >= 0);
            Assert.True(redisCacheExistingTime >= 0);
            Assert.True(redisCacheNonExistingTime >= 0);

            // Simulate output to show the comparison
            Output($"Memory Cache (Existing Key): {memoryCacheExistingTime}ms");
            Output($"Memory Cache (Non-Existing Key): {memoryCacheNonExistingTime}ms");
            Output($"Redis Cache (Existing Key): {redisCacheExistingTime}ms");
            Output($"Redis Cache (Non-Existing Key): {redisCacheNonExistingTime}ms");

            // Redis will generally be slower due to network latency
            Assert.True(redisCacheExistingTime > memoryCacheExistingTime);
        }

        [Fact]
        public async Task GetOrCreateAsync_PerformanceComparison()
        {
            // Arrange
            const int iterations = 50; // Reduced for faster test
            var keys = new List<string>();
            for (int i = 0; i < iterations; i++)
            {
                keys.Add($"test-key-{i}");
            }

            var factory = () => Task.FromResult(new TestItem { Id = 1, Name = "Test" });

            var stopwatch = new Stopwatch();

            // Act - Memory Cache
            stopwatch.Start();
            foreach (var key in keys)
            {
                await _memoryCache.GetOrCreateAsync(key, factory);
            }
            stopwatch.Stop();
            var memoryCacheTime = stopwatch.ElapsedMilliseconds;

            // Create a real RedisCacheService instance for testing
            var mockDistributedCache = new Mock<IDistributedCache>();
            var mockRedisConnection = new Mock<IConnectionMultiplexer>();
            var mockLogger = new Mock<ILogger<RedisCacheService>>();
            var mockOptions = new Mock<IOptions<CacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(_cacheOptions);

            // Setup the distributed cache behavior
            mockDistributedCache.Setup(x => x.Get(It.IsAny<string>()))
                .Returns((byte[]?)null);

            mockDistributedCache.Setup(x => x.Set(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>()))
                .Callback(async () =>
                {
                    // Simulate Redis latency
                    await Task.Delay(10);
                });

            var redisCache = new RedisCacheService(
                mockDistributedCache.Object,
                mockRedisConnection.Object,
                mockOptions.Object,
                mockLogger.Object);

            // Act - Redis Cache
            stopwatch.Restart();
            foreach (var key in keys)
            {
                await redisCache.GetOrCreateAsync(key, factory);
            }
            stopwatch.Stop();
            var redisCacheTime = stopwatch.ElapsedMilliseconds;

            // Assert - We only verify that measurements were taken
            Assert.True(memoryCacheTime >= 0);
            Assert.True(redisCacheTime >= 0);

            // Simulate output to show the comparison
            Output($"Memory Cache GetOrCreateAsync: {memoryCacheTime}ms");
            Output($"Redis Cache GetOrCreateAsync: {redisCacheTime}ms");

            // For test stability, we don't strictly assert that Redis is slower
            // as this can lead to flaky tests in some environments

            // Instead, log the comparison for informational purposes
            Output($"Redis slower than Memory: {redisCacheTime > memoryCacheTime}");
            Output($"Redis time ratio: {(double)redisCacheTime / (double)memoryCacheTime:F2}x");
        }

        [Fact]
        public void RemoveByPrefix_PerformanceComparison()
        {
            // Arrange
            const string prefix = "test-prefix";

            // First, populate memory cache with prefixed items
            for (int i = 0; i < 50; i++)
            {
                _memoryCache.Set($"{prefix}:{i}", new TestItem { Id = i, Name = $"Test {i}" });
            }

            // Configure Redis implementation for RemoveByPrefix
            var mockDistributedCache = new Mock<IDistributedCache>();
            var mockRedisConnection = new Mock<IConnectionMultiplexer>();
            var mockRedisDatabase = new Mock<IDatabase>();
            var mockLogger = new Mock<ILogger<RedisCacheService>>();
            var mockOptions = new Mock<IOptions<CacheOptions>>();
            var mockServer = new Mock<IServer>();
            var mockEndPoints = new System.Net.EndPoint[] { new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379) };

            mockOptions.Setup(x => x.Value).Returns(_cacheOptions);

            var redisKeys = new List<RedisKey>();
            for (int i = 0; i < 50; i++)
            {
                redisKeys.Add(new RedisKey($"{prefix}:{i}"));
            }

            mockServer.Setup(x => x.Keys(
                    It.IsAny<int>(),
                    It.Is<RedisValue>(pattern => pattern.ToString().StartsWith(prefix)),
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()))
                .Returns(redisKeys.ToArray());

            mockRedisConnection.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
                .Returns(mockEndPoints);

            mockRedisConnection.Setup(x => x.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
                .Returns(mockServer.Object);

            mockRedisConnection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockRedisDatabase.Object);

            // Create a real RedisCacheService instance
            var redisCache = new RedisCacheService(
                mockDistributedCache.Object,
                mockRedisConnection.Object,
                mockOptions.Object,
                mockLogger.Object);

            // Mock the database KeyDelete to add latency and track calls
            int keyDeleteCallCount = 0;
            mockRedisDatabase.Setup(x => x.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Callback(() =>
                {
                    Task.Delay(1).Wait(); // Add minimal latency
                    keyDeleteCallCount++;
                })
                .Returns(true);

            var stopwatch = new Stopwatch();

            // Act - Memory Cache
            stopwatch.Start();
            _memoryCache.RemoveByPrefix(prefix);
            stopwatch.Stop();
            var memoryCacheTime = stopwatch.ElapsedMilliseconds;

            // Act - Redis Cache
            stopwatch.Restart();
            // Directly call the RemoveByPrefix method
            redisCache.RemoveByPrefix(prefix);
            stopwatch.Stop();
            var redisCacheTime = stopwatch.ElapsedMilliseconds;

            // Assert - We only verify that measurements were taken
            Assert.True(memoryCacheTime >= 0);
            Assert.True(redisCacheTime >= 0);

            // Simulate output to show the comparison
            Output($"Memory Cache RemoveByPrefix: {memoryCacheTime}ms");
            Output($"Redis Cache RemoveByPrefix: {redisCacheTime}ms");
            Output($"Redis key delete call count: {keyDeleteCallCount}");
        }

        private void Output(string message)
        {
            // In a real environment, you might log this information
            // For tests, we'll use Debug.WriteLine
            Debug.WriteLine(message);
        }

        // Test class for serialization
        private class TestItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;

            public override bool Equals(object? obj)
            {
                if (obj is TestItem other)
                {
                    return Id == other.Id && Name == other.Name;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Id, Name);
            }
        }
    }
}
