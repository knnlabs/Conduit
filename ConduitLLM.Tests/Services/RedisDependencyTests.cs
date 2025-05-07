using System;
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
    // Special wrapper class for testing that inherits from RedisConnectionFactory but allows mocking
    public class TestableRedisConnectionFactory : RedisConnectionFactory
    {
        private readonly Func<Task<IConnectionMultiplexer>> _getConnectionImplementation;
        private readonly Func<string, Task<IConnectionMultiplexer>> _getConnectionWithStringImplementation;

        public TestableRedisConnectionFactory(
            IOptions<CacheOptions> options,
            ILogger<RedisConnectionFactory> logger,
            Func<Task<IConnectionMultiplexer>> getConnectionImpl,
            Func<string, Task<IConnectionMultiplexer>>? getConnectionWithStringImpl = null)
            : base(options, logger)
        {
            _getConnectionImplementation = getConnectionImpl;
            _getConnectionWithStringImplementation = getConnectionWithStringImpl ?? 
                (s => _getConnectionImplementation());
        }

        public override Task<IConnectionMultiplexer> GetConnectionAsync()
        {
            return _getConnectionImplementation();
        }

        public override Task<IConnectionMultiplexer> GetConnectionAsync(string connectionString)
        {
            return _getConnectionWithStringImplementation(connectionString);
        }
    }
    
    public class RedisDependencyTests
    {
        private readonly Mock<IOptions<CacheOptions>> _mockCacheOptions;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<IDistributedCache> _mockDistributedCache;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<RedisConnectionFactory>> _mockRedisConnectionLogger;
        private readonly Mock<ILogger<RedisCacheService>> _mockRedisCacheLogger;
        private readonly Mock<ILogger<CacheServiceFactory>> _mockFactoryLogger;
        private readonly Mock<ILogger<NullCacheService>> _mockNullCacheLogger;
        private readonly Mock<ILogger<CacheService>> _mockCacheServiceLogger;
        private readonly CacheOptions _cacheOptions;

        public RedisDependencyTests()
        {
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
            
            _mockCacheOptions = new Mock<IOptions<CacheOptions>>();
            _mockCacheOptions.Setup(x => x.Value).Returns(_cacheOptions);
            
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockDistributedCache = new Mock<IDistributedCache>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockRedisConnectionLogger = new Mock<ILogger<RedisConnectionFactory>>();
            _mockRedisCacheLogger = new Mock<ILogger<RedisCacheService>>();
            _mockFactoryLogger = new Mock<ILogger<CacheServiceFactory>>();
            _mockNullCacheLogger = new Mock<ILogger<NullCacheService>>();
            _mockCacheServiceLogger = new Mock<ILogger<CacheService>>();
            
            // Use Setup with a string parameter instead of generic extension method
            _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());
                
            // Setup specific loggers by name instead of using generic extension methods
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(NullCacheService).FullName!))
                .Returns(_mockNullCacheLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(RedisCacheService).FullName!))
                .Returns(_mockRedisCacheLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(CacheService).FullName!))
                .Returns(_mockCacheServiceLogger.Object);
            _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(CacheServiceFactory).FullName!))
                .Returns(_mockFactoryLogger.Object);
        }

        [Fact]
        public async Task CacheServiceFactory_WhenRedisConnectionFails_FallsBackToMemoryCache()
        {
            // Arrange
            var connectionException = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed");
            
            // Create a testable factory that throws an exception
            var redisConnectionFactory = new TestableRedisConnectionFactory(
                _mockCacheOptions.Object,
                _mockRedisConnectionLogger.Object,
                () => Task.FromException<IConnectionMultiplexer>(connectionException));
            
            var factory = new CacheServiceFactory(
                _mockCacheOptions.Object,
                _mockMemoryCache.Object,
                _mockDistributedCache.Object,
                _mockLoggerFactory.Object,
                redisConnectionFactory);
            
            // Act
            var cacheService = await factory.CreateCacheServiceAsync();
            
            // Assert
            Assert.NotNull(cacheService);
            Assert.IsType<CacheService>(cacheService);
            _mockFactoryLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.Is<Exception>(e => e == connectionException),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task CacheServiceFactory_WhenCacheIsDisabled_ReturnsNullCacheService()
        {
            // Arrange
            var disabledOptions = new CacheOptions
            {
                IsEnabled = false
            };
            
            var mockDisabledOptions = new Mock<IOptions<CacheOptions>>();
            mockDisabledOptions.Setup(x => x.Value).Returns(disabledOptions);
            
            // Create a testable factory
            var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
            var redisConnectionFactory = new TestableRedisConnectionFactory(
                _mockCacheOptions.Object,
                _mockRedisConnectionLogger.Object,
                () => Task.FromResult(mockConnectionMultiplexer.Object));
            
            var factory = new CacheServiceFactory(
                mockDisabledOptions.Object,
                _mockMemoryCache.Object,
                _mockDistributedCache.Object,
                _mockLoggerFactory.Object,
                redisConnectionFactory);
            
            // Act
            var cacheService = await factory.CreateCacheServiceAsync();
            
            // Assert
            Assert.NotNull(cacheService);
            Assert.IsType<NullCacheService>(cacheService);
        }
        
        [Fact]
        public async Task CacheServiceFactory_WhenRedisIsConfigured_ReturnsRedisCacheService()
        {
            // Arrange
            var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
            
            // Create a testable factory that returns a connection
            var redisConnectionFactory = new TestableRedisConnectionFactory(
                _mockCacheOptions.Object,
                _mockRedisConnectionLogger.Object,
                () => Task.FromResult(mockConnectionMultiplexer.Object));
            
            var factory = new CacheServiceFactory(
                _mockCacheOptions.Object,
                _mockMemoryCache.Object,
                _mockDistributedCache.Object,
                _mockLoggerFactory.Object,
                redisConnectionFactory);
            
            // Act
            var cacheService = await factory.CreateCacheServiceAsync();
            
            // Assert
            Assert.NotNull(cacheService);
            Assert.IsType<RedisCacheService>(cacheService);
        }
        
        [Fact]
        public async Task CacheServiceFactory_WhenMemoryCacheIsConfigured_ReturnsCacheService()
        {
            // Arrange
            var memoryCacheOptions = new CacheOptions
            {
                IsEnabled = true,
                CacheType = "Memory"
            };
            
            var mockMemoryCacheOptions = new Mock<IOptions<CacheOptions>>();
            mockMemoryCacheOptions.Setup(x => x.Value).Returns(memoryCacheOptions);
            
            // Create a testable factory
            var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
            var redisConnectionFactory = new TestableRedisConnectionFactory(
                _mockCacheOptions.Object,
                _mockRedisConnectionLogger.Object,
                () => Task.FromResult(mockConnectionMultiplexer.Object));
            
            var factory = new CacheServiceFactory(
                mockMemoryCacheOptions.Object,
                _mockMemoryCache.Object,
                _mockDistributedCache.Object,
                _mockLoggerFactory.Object,
                redisConnectionFactory);
            
            // Act
            var cacheService = await factory.CreateCacheServiceAsync();
            
            // Assert
            Assert.NotNull(cacheService);
            Assert.IsType<CacheService>(cacheService);
        }
    }
}