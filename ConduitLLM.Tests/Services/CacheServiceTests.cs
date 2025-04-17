using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class CacheServiceTests
    {
        private readonly ICacheService _cacheService;
        private readonly Mock<IMemoryCache> _memoryCacheMock;
        private readonly MemoryCache _memoryCache;
        private readonly Mock<IOptions<CacheOptions>> _cacheOptionsMock;
        private readonly CacheOptions _cacheOptions;
        private readonly object _cacheKey = new();

        public CacheServiceTests()
        {
            // Create a real MemoryCache for testing since mocking Set extension method is problematic
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            
            // Still create a mock for verification purposes where needed
            _memoryCacheMock = new Mock<IMemoryCache>();
            
            // For TryGetValue we can use mock to track calls
            _memoryCacheMock
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
                .Returns(new TryGetValueCallback((object key, out object? value) => 
                {
                    return _memoryCache.TryGetValue(key, out value!);
                }));

            // Setup CreateEntry to delegate to the real memory cache
            _memoryCacheMock
                .Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Returns((object key) => _memoryCache.CreateEntry(key));

            // For Remove we delegate to the real cache but also verify
            _memoryCacheMock
                .Setup(m => m.Remove(It.IsAny<object>()))
                .Callback<object>(key => _memoryCache.Remove(key));

            // Setup cache options
            _cacheOptions = new CacheOptions
            {
                DefaultAbsoluteExpirationMinutes = 60,
                DefaultSlidingExpirationMinutes = 20,
                UseDefaultExpirationTimes = true
            };
            _cacheOptionsMock = new Mock<IOptions<CacheOptions>>();
            _cacheOptionsMock.Setup(m => m.Value).Returns(_cacheOptions);

            // Create the service with mocked dependencies
            _cacheService = new CacheService(_memoryCacheMock.Object, _cacheOptionsMock.Object);
        }

        [Fact]
        public void Get_ReturnsCachedValue_WhenKeyExists()
        {
            // Arrange
            string key = "testKey";
            string expectedValue = "testValue";
            _memoryCache.Set(key, expectedValue);

            // Act
            var result = _cacheService.Get<string>(key);

            // Assert
            Assert.Equal(expectedValue, result);
            _memoryCacheMock.Verify(m => m.TryGetValue(key, out It.Ref<object?>.IsAny), Times.Once);
        }

        [Fact]
        public void Get_ReturnsDefault_WhenKeyDoesNotExist()
        {
            // Arrange
            string key = "nonExistentKey";
            
            // Make sure the key doesn't exist
            _memoryCache.Remove(key);

            // Act
            var result = _cacheService.Get<string>(key);

            // Assert
            Assert.Null(result);
            _memoryCacheMock.Verify(m => m.TryGetValue(key, out It.Ref<object?>.IsAny), Times.Once);
        }

        [Fact]
        public void Set_StoresValueInCache()
        {
            // Arrange
            string key = "testKey";
            string expectedValue = "testValue";
            var absoluteExpiration = TimeSpan.FromMinutes(10);
            var slidingExpiration = TimeSpan.FromMinutes(5);

            // Act
            _cacheService.Set(key, expectedValue, absoluteExpiration, slidingExpiration);

            // Assert
            var cachedValue = _memoryCache.Get<string>(key);
            Assert.Equal(expectedValue, cachedValue);
        }

        [Fact]
        public void Set_StoresValueInCache_WithNoExpirations()
        {
            // Arrange
            string key = "testKey";
            string expectedValue = "testValue";

            // Act
            _cacheService.Set(key, expectedValue);

            // Assert
            var cachedValue = _memoryCache.Get<string>(key);
            Assert.Equal(expectedValue, cachedValue);
        }

        [Fact]
        public void Remove_RemovesValueFromCache()
        {
            // Arrange
            string key = "testKey";
            string expectedValue = "testValue";
            _memoryCache.Set(key, expectedValue);
            
            // Verify it's in the cache initially
            Assert.Equal(expectedValue, _memoryCache.Get<string>(key));

            // Act
            _cacheService.Remove(key);

            // Assert
            _memoryCacheMock.Verify(m => m.Remove(key), Times.Once);
            Assert.Null(_memoryCache.Get<string>(key));
        }

        [Fact]
        public async Task GetOrCreateAsync_ReturnsCachedValue_WhenKeyExists()
        {
            // Arrange
            string key = "testKey";
            string expectedValue = "testValue";
            _memoryCache.Set(key, expectedValue);
            int factoryCallCount = 0;
            
            // Act
            var result = await _cacheService.GetOrCreateAsync<string>(
                key, 
                () => 
                {
                    factoryCallCount++;
                    return Task.FromResult("factoryValue");
                });

            // Assert
            Assert.Equal(expectedValue, result);
            Assert.Equal(0, factoryCallCount);
            _memoryCacheMock.Verify(m => m.TryGetValue(key, out It.Ref<object?>.IsAny), Times.Once);
        }

        [Fact]
        public async Task GetOrCreateAsync_CreatesAndCachesValue_WhenKeyDoesNotExist()
        {
            // Arrange
            string key = "nonExistentKey";
            string factoryValue = "newValue";
            _memoryCache.Remove(key); // Ensure key doesn't exist
            
            var absoluteExpiration = TimeSpan.FromMinutes(10);
            var slidingExpiration = TimeSpan.FromMinutes(5);

            // Act
            var result = await _cacheService.GetOrCreateAsync<string>(
                key, 
                () => Task.FromResult(factoryValue),
                absoluteExpiration,
                slidingExpiration);

            // Assert
            Assert.Equal(factoryValue, result);
            Assert.Equal(factoryValue, _memoryCache.Get<string>(key));
        }

        [Fact]
        public void RemoveByPrefix_IsNotImplemented()
        {
            // Arrange
            string prefix = "test";

            // Act & Assert - Should not throw
            _cacheService.RemoveByPrefix(prefix);
            
            // No real way to verify behavior since it's not implemented
        }
    }

    // Custom delegate to handle the TryGetValue callback with proper null handling
    public delegate bool TryGetValueCallback(object key, out object? value);
}
