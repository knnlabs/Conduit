using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using Xunit;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class RedisIntegrationTests
    {
        private readonly string _redisConnectionString = "localhost:6379"; // Change this to your Redis server
        private readonly bool _redisServerAvailable;
        private RedisCacheService? _redisCacheService;
        private IConnectionMultiplexer? _connectionMultiplexer;
        private RedisCache? _distributedCache;
        
        public RedisIntegrationTests()
        {
            // Check if Redis server is available
            _redisServerAvailable = IsRedisAvailable();
            
            if (_redisServerAvailable)
            {
                SetupRedisCache();
            }
        }
        
        private bool IsRedisAvailable()
        {
            try
            {
                var connection = ConnectionMultiplexer.Connect(_redisConnectionString);
                connection.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private void SetupRedisCache()
        {
            var cacheOptions = new CacheOptions
            {
                IsEnabled = true,
                CacheType = "Redis",
                DefaultExpirationMinutes = 5,
                DefaultAbsoluteExpirationMinutes = 10,
                DefaultSlidingExpirationMinutes = 2,
                RedisConnectionString = _redisConnectionString,
                RedisInstanceName = "integration-test"
            };
            
            var mockOptions = new Mock<IOptions<CacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(cacheOptions);
            
            var redisOptions = new RedisCacheOptions
            {
                Configuration = _redisConnectionString,
                InstanceName = cacheOptions.RedisInstanceName
            };
            
            var redisOptionsWrapper = new Mock<IOptions<RedisCacheOptions>>();
            redisOptionsWrapper.Setup(x => x.Value).Returns(redisOptions);
            
            _distributedCache = new RedisCache(redisOptionsWrapper.Object);
            _connectionMultiplexer = ConnectionMultiplexer.Connect(_redisConnectionString);
            
            var loggerFactory = new LoggerFactory();
            
            _redisCacheService = new RedisCacheService(
                _distributedCache,
                _connectionMultiplexer,
                mockOptions.Object,
                loggerFactory.CreateLogger<RedisCacheService>());
        }
        
        [Fact(Skip = "Requires Redis server")]
        public void Skip_If_Redis_Not_Available()
        {
            // This test is skipped by default
            Assert.True(_redisServerAvailable, "Redis server should be available for integration tests");
        }
        
        [Fact(Skip = "Requires Redis server")]
        public void Redis_SetAndGet()
        {
            // This test is skipped by default
            
            // Arrange
            var key = "integration-test-key-" + Guid.NewGuid();
            var value = new TestItem { Id = 42, Name = "Integration Test" };
            
            try
            {
                // Act
                _redisCacheService!.Set(key, value);
                var result = _redisCacheService.Get<TestItem>(key);
                
                // Assert
                Assert.NotNull(result);
                Assert.Equal(value.Id, result.Id);
                Assert.Equal(value.Name, result.Name);
            }
            finally
            {
                // Cleanup
                _redisCacheService!.Remove(key);
            }
        }
        
        [Fact(Skip = "Requires Redis server")]
        public async Task Redis_GetOrCreateAsync()
        {
            // This test is skipped by default
            
            // Arrange
            var key = "integration-test-getoradd-" + Guid.NewGuid();
            var value = new TestItem { Id = 43, Name = "GetOrCreateAsync Test" };
            
            try
            {
                // Act
                var result1 = await _redisCacheService!.GetOrCreateAsync(key, 
                    () => Task.FromResult(value));
                    
                // Should get from cache, not call factory again
                var factoryCalled = false;
                var result2 = await _redisCacheService!.GetOrCreateAsync(key,
                    () => {
                        factoryCalled = true;
                        return Task.FromResult(new TestItem { Id = 999, Name = "Should Not Be Used" });
                    });
                
                // Assert
                Assert.NotNull(result1);
                Assert.Equal(value.Id, result1.Id);
                Assert.Equal(value.Name, result1.Name);
                
                Assert.NotNull(result2);
                Assert.Equal(value.Id, result2.Id);
                Assert.Equal(value.Name, result2.Name);
                Assert.False(factoryCalled, "Factory should not be called for cached items");
            }
            finally
            {
                // Cleanup
                _redisCacheService!.Remove(key);
            }
        }
        
        [Fact(Skip = "Requires Redis server")]
        public void Redis_RemoveByPrefix()
        {
            // This test is skipped by default
            
            // Arrange
            var prefix = "integration-test-prefix-" + Guid.NewGuid();
            var keys = new[]
            {
                $"{prefix}:key1",
                $"{prefix}:key2",
                $"{prefix}:key3",
                $"{prefix}:subkey:key4"
            };
            
            try
            {
                // Populate cache with test data
                foreach (var key in keys)
                {
                    _redisCacheService!.Set(key, new TestItem { Id = 100, Name = key });
                }
                
                // Verify keys exist
                foreach (var key in keys)
                {
                    var value = _redisCacheService!.Get<TestItem>(key);
                    Assert.NotNull(value);
                }
                
                // Act
                _redisCacheService!.RemoveByPrefix(prefix);
                
                // Assert - keys should be removed
                foreach (var key in keys)
                {
                    var value = _redisCacheService!.Get<TestItem>(key);
                    Assert.Null(value);
                }
            }
            finally
            {
                // Cleanup
                foreach (var key in keys)
                {
                    _redisCacheService?.Remove(key);
                }
            }
        }
        
        // Test class for serialization
        private class TestItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}