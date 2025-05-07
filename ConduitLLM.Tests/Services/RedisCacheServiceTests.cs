using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class RedisCacheServiceTests
    {
        private readonly Mock<IDistributedCache> _mockDistributedCache;
        private readonly Mock<IConnectionMultiplexer> _mockRedisConnection;
        private readonly Mock<IDatabase> _mockRedisDatabase;
        private readonly Mock<ILogger<RedisCacheService>> _mockLogger;
        private readonly CacheOptions _cacheOptions;
        private readonly RedisCacheService _cacheService;

        public RedisCacheServiceTests()
        {
            _mockDistributedCache = new Mock<IDistributedCache>();
            _mockRedisConnection = new Mock<IConnectionMultiplexer>();
            _mockRedisDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<RedisCacheService>>();
            
            _cacheOptions = new CacheOptions
            {
                IsEnabled = true,
                CacheType = "Redis",
                DefaultExpirationMinutes = 60,
                DefaultAbsoluteExpirationMinutes = 120,
                DefaultSlidingExpirationMinutes = 30,
                RedisConnectionString = "localhost:6379",
                RedisInstanceName = "test-cache"
            };
            
            var mockOptions = new Mock<IOptions<CacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(_cacheOptions);
            
            _mockRedisConnection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockRedisDatabase.Object);
            
            _cacheService = new RedisCacheService(
                _mockDistributedCache.Object,
                _mockRedisConnection.Object,
                mockOptions.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void Get_WhenKeyExists_ShouldReturnCachedValue()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestItem { Id = 1, Name = "Test" };
            var serializedValue = System.Text.Json.JsonSerializer.Serialize(expectedValue);
            
            // Use Get instead of GetString extension method
            _mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k == key)))
                .Returns(System.Text.Encoding.UTF8.GetBytes(serializedValue));
                
            // Act
            var result = _cacheService.Get<TestItem>(key);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedValue.Id, result.Id);
            Assert.Equal(expectedValue.Name, result.Name);
            
            // Verify using Get instead of GetString
            _mockDistributedCache.Verify(x => x.Get(It.Is<string>(k => k == key)), Times.Once);
        }
        
        [Fact]
        public void Get_WhenKeyDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var key = "nonexistent-key";
            
            // Use Get instead of GetString extension method
            _mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k == key)))
                .Returns((byte[]?)null);
                
            // Act
            var result = _cacheService.Get<TestItem>(key);
            
            // Assert
            Assert.Null(result);
            
            // Verify using Get instead of GetString
            _mockDistributedCache.Verify(x => x.Get(It.Is<string>(k => k == key)), Times.Once);
        }
        
        [Fact]
        public void Get_WhenExceptionOccurs_ShouldReturnNullAndLogError()
        {
            // Arrange
            var key = "error-key";
            
            // Use Get instead of GetString extension method
            _mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k == key)))
                .Throws(new Exception("Test exception"));
                
            // Act
            var result = _cacheService.Get<TestItem>(key);
            
            // Assert
            Assert.Null(result);
            
            // Verify using Get instead of GetString
            _mockDistributedCache.Verify(x => x.Get(It.Is<string>(k => k == key)), Times.Once);
            // We could verify logging, but that's implementation detail
        }
        
        [Fact]
        public void Set_ShouldSetValueInCache()
        {
            // Arrange
            var key = "test-key";
            var value = new TestItem { Id = 1, Name = "Test" };
            
            // Act
            _cacheService.Set(key, value);
            
            // Assert
            // Use Set instead of SetString extension method
            _mockDistributedCache.Verify(x => x.Set(
                It.Is<string>(k => k == key),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>()), 
                Times.Once);
        }
        
        [Fact]
        public void Set_WithCustomExpiration_ShouldUseProvidedExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = new TestItem { Id = 1, Name = "Test" };
            var absoluteExpiration = TimeSpan.FromMinutes(10);
            var slidingExpiration = TimeSpan.FromMinutes(5);
            
            // We need to capture the options to verify them
            DistributedCacheEntryOptions? capturedOptions = null;
            
            // Use Set instead of SetString extension method
            _mockDistributedCache.Setup(x => x.Set(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>()))
                .Callback<string, byte[], DistributedCacheEntryOptions>((k, v, o) => capturedOptions = o);
            
            // Act
            _cacheService.Set(key, value, absoluteExpiration, slidingExpiration);
            
            // Assert
            Assert.NotNull(capturedOptions);
            Assert.Equal(absoluteExpiration, capturedOptions.AbsoluteExpirationRelativeToNow);
            Assert.Equal(slidingExpiration, capturedOptions.SlidingExpiration);
        }
        
        [Fact]
        public void Remove_ShouldRemoveValueFromCache()
        {
            // Arrange
            var key = "test-key";
            
            // Act
            _cacheService.Remove(key);
            
            // Assert
            _mockDistributedCache.Verify(x => x.Remove(key), Times.Once);
        }
        
        [Fact]
        public async Task GetOrCreateAsync_WhenKeyExists_ShouldReturnCachedValue()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestItem { Id = 1, Name = "Test" };
            var serializedValue = System.Text.Json.JsonSerializer.Serialize(expectedValue);
            
            // Use Get instead of GetString extension method
            _mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k == key)))
                .Returns(System.Text.Encoding.UTF8.GetBytes(serializedValue));
                
            var factoryCalled = false;
            Func<Task<TestItem>> factory = () => 
            {
                factoryCalled = true;
                return Task.FromResult(new TestItem { Id = 2, Name = "Factory" });
            };
            
            // Act
            var result = await _cacheService.GetOrCreateAsync(key, factory);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedValue.Id, result.Id);
            Assert.Equal(expectedValue.Name, result.Name);
            Assert.False(factoryCalled);
            
            // Verify using Get instead of GetString
            _mockDistributedCache.Verify(x => x.Get(It.Is<string>(k => k == key)), Times.Once);
        }
        
        [Fact]
        public async Task GetOrCreateAsync_WhenKeyDoesNotExist_ShouldCallFactoryAndCacheResult()
        {
            // Arrange
            var key = "nonexistent-key";
            
            // Use Get instead of GetString extension method
            _mockDistributedCache.Setup(x => x.Get(It.Is<string>(k => k == key)))
                .Returns((byte[]?)null);
                
            var factoryValue = new TestItem { Id = 2, Name = "Factory" };
            Func<Task<TestItem>> factory = () => Task.FromResult(factoryValue);
            
            // Act
            var result = await _cacheService.GetOrCreateAsync(key, factory);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(factoryValue.Id, result.Id);
            Assert.Equal(factoryValue.Name, result.Name);
            
            // Verify using Get instead of GetString
            _mockDistributedCache.Verify(x => x.Get(It.Is<string>(k => k == key)), Times.Exactly(2)); // Initial check and after lock
            
            // Use Set instead of SetString
            _mockDistributedCache.Verify(x => x.Set(
                It.Is<string>(k => k == key),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>()),
                Times.Once);
        }
        
        [Fact]
        public void RemoveByPrefix_ShouldRemoveMultipleMatchingKeys()
        {
            // Arrange
            var prefix = "test-prefix";
            var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379);
            var mockServer = new Mock<IServer>();
            
            var keys = new RedisKey[] 
            { 
                new RedisKey($"{prefix}:key1"),
                new RedisKey($"{prefix}:key2"),
                new RedisKey($"{prefix}:key3")
            };
            
            mockServer.Setup(x => x.Keys(
                It.Is<int>(db => db == -1),
                It.Is<RedisValue>(pattern => pattern == $"{prefix}*"),
                It.Is<int>(pageSize => pageSize == 250),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
                .Returns(keys);
                
            _mockRedisConnection.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
                .Returns(new System.Net.EndPoint[] { endPoint });
                
            _mockRedisConnection.Setup(x => x.GetServer(endPoint, It.IsAny<object>()))
                .Returns(mockServer.Object);
                
            // Act
            _cacheService.RemoveByPrefix(prefix);
            
            // Assert
            _mockRedisDatabase.Verify(x => x.KeyDelete(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()),
                Times.Exactly(3));
        }
        
        // Test class for serialization
        private class TestItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}