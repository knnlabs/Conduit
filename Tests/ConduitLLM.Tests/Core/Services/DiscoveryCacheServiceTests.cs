using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    public class DiscoveryCacheServiceTests
    {
        private readonly Mock<IDistributedCache> _mockDistributedCache;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<ILogger<DiscoveryCacheService>> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly DiscoveryCacheOptions _options;
        private readonly DiscoveryCacheService _service;

        public DiscoveryCacheServiceTests()
        {
            _mockDistributedCache = new Mock<IDistributedCache>();
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<DiscoveryCacheService>>();
            _mockServiceProvider = new Mock<IServiceProvider>();

            _options = new DiscoveryCacheOptions
            {
                EnableCaching = true,
                CacheDurationMinutes = 360,
                WarmCacheOnStartup = false
            };

            var mockOptions = new Mock<IOptions<DiscoveryCacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(_options);

            // Setup service provider to return distributed cache
            _mockServiceProvider
                .Setup(x => x.GetService(typeof(IDistributedCache)))
                .Returns(_mockDistributedCache.Object);

            _service = new DiscoveryCacheService(
                mockOptions.Object,
                _mockMemoryCache.Object,
                _mockLogger.Object,
                _mockServiceProvider.Object);
        }

        [Fact]
        public async Task GetDiscoveryResultsAsync_Should_Return_Null_When_Caching_Disabled()
        {
            // Arrange
            _options.EnableCaching = false;
            var service = CreateServiceWithOptions(_options);

            // Act
            var result = await service.GetDiscoveryResultsAsync("test-key");

            // Assert
            Assert.Null(result);
            _mockDistributedCache.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockMemoryCache.Verify(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny), Times.Never);
        }

        [Fact]
        public async Task GetDiscoveryResultsAsync_Should_Return_From_Redis_When_Available()
        {
            // Arrange
            var cacheKey = "discovery:models:all";
            var expectedResult = new DiscoveryModelsResult
            {
                Data = new List<object> { new { id = "gpt-4", provider = "openai" } },
                Count = 1,
                CachedAt = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(expectedResult, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            _mockDistributedCache
                .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.GetDiscoveryResultsAsync(cacheKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            _mockDistributedCache.Verify(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
            _mockMemoryCache.Verify(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny), Times.Never);
        }

        [Fact]
        public async Task GetDiscoveryResultsAsync_Should_Fallback_To_Memory_Cache_When_Redis_Empty()
        {
            // Arrange
            var cacheKey = "discovery:models:all";
            var expectedResult = new DiscoveryModelsResult
            {
                Data = new List<object> { new { id = "claude-3", provider = "anthropic" } },
                Count = 1,
                CachedAt = DateTime.UtcNow
            };

            _mockDistributedCache
                .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);

            object memoryResult = expectedResult;
            _mockMemoryCache
                .Setup(x => x.TryGetValue(cacheKey, out memoryResult))
                .Returns(true);

            // Act
            var result = await _service.GetDiscoveryResultsAsync(cacheKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            _mockDistributedCache.Verify(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
            _mockMemoryCache.Verify(x => x.TryGetValue(cacheKey, out It.Ref<object>.IsAny), Times.Once);
        }

        [Fact]
        public async Task SetDiscoveryResultsAsync_Should_Not_Cache_When_Caching_Disabled()
        {
            // Arrange
            _options.EnableCaching = false;
            var service = CreateServiceWithOptions(_options);
            var results = new DiscoveryModelsResult { Data = new List<object> { new { id = "test" } }, Count = 1 };

            // Act
            await service.SetDiscoveryResultsAsync("test-key", results);

            // Assert
            _mockDistributedCache.Verify(
                x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), 
                Times.Never);
            _mockMemoryCache.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task SetDiscoveryResultsAsync_Should_Cache_In_Both_Redis_And_Memory()
        {
            // Arrange
            var cacheKey = "discovery:models:capability:chat";
            var results = new DiscoveryModelsResult
            {
                Data = new List<object> { new { id = "gpt-4", provider = "openai" } },
                Count = 1
            };

            var memoryCacheEntry = new Mock<Microsoft.Extensions.Caching.Memory.ICacheEntry>();
            memoryCacheEntry.SetupAllProperties();
            _mockMemoryCache
                .Setup(x => x.CreateEntry(cacheKey))
                .Returns(memoryCacheEntry.Object);

            // Act
            await _service.SetDiscoveryResultsAsync(cacheKey, results);

            // Assert
            _mockDistributedCache.Verify(
                x => x.SetAsync(
                    cacheKey, 
                    It.IsAny<byte[]>(), 
                    It.Is<DistributedCacheEntryOptions>(opts => 
                        opts.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(360)), 
                    It.IsAny<CancellationToken>()), 
                Times.Once);

            _mockMemoryCache.Verify(x => x.CreateEntry(cacheKey), Times.Once);
            Assert.True(results.CachedAt != default);
        }

        [Fact]
        public async Task InvalidateAllDiscoveryAsync_Should_Remove_Common_Patterns_From_Redis()
        {
            // Arrange
            var expectedPatterns = new[]
            {
                "discovery:models:all",
                "discovery:models:capability:chat",
                "discovery:models:capability:vision",
                "discovery:models:capability:image_generation",
                "discovery:models:capability:video_generation",
                "discovery:models:capability:audio_transcription",
                "discovery:models:capability:text_to_speech",
                "discovery:models:capability:embeddings",
                "discovery:models:capability:function_calling"
            };

            // Act
            await _service.InvalidateAllDiscoveryAsync();

            // Assert
            foreach (var pattern in expectedPatterns)
            {
                _mockDistributedCache.Verify(
                    x => x.RemoveAsync(pattern, It.IsAny<CancellationToken>()), 
                    Times.Once,
                    $"Should remove pattern: {pattern}");
            }
        }

        [Fact]
        public async Task InvalidateAllDiscoveryAsync_Should_Handle_Redis_Errors_Gracefully()
        {
            // Arrange
            _mockDistributedCache
                .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection failed"));

            // Act (should not throw)
            await _service.InvalidateAllDiscoveryAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error invalidating all discovery cache entries")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvalidatePatternAsync_Should_Remove_Specific_Pattern()
        {
            // Arrange
            var pattern = "discovery:models:capability:custom";

            // Act
            await _service.InvalidatePatternAsync(pattern);

            // Assert
            _mockDistributedCache.Verify(
                x => x.RemoveAsync(pattern, It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task InvalidatePatternAsync_Should_Call_InvalidateAll_For_Wildcard()
        {
            // Arrange
            var pattern = "discovery:models:*";

            // Act
            await _service.InvalidatePatternAsync(pattern);

            // Assert
            // Should invalidate all common patterns when wildcard is used
            _mockDistributedCache.Verify(
                x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
                Times.AtLeast(9)); // At least the 9 common patterns
        }

        [Fact]
        public async Task GetStatisticsAsync_Should_Return_Correct_Stats()
        {
            // Arrange
            // Simulate some cache hits and misses
            var cacheKey = "test-key";
            _mockDistributedCache
                .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);
            
            object nullResult = null;
            _mockMemoryCache
                .Setup(x => x.TryGetValue(cacheKey, out nullResult))
                .Returns(false);

            // Generate a miss
            await _service.GetDiscoveryResultsAsync(cacheKey);

            // Setup for a hit
            var result = new DiscoveryModelsResult { Data = new List<object>(), Count = 0 };
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var bytes2 = System.Text.Encoding.UTF8.GetBytes(json);
            _mockDistributedCache
                .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bytes2);

            // Generate a hit
            await _service.GetDiscoveryResultsAsync(cacheKey);

            // Act
            var stats = await _service.GetStatisticsAsync();

            // Assert
            Assert.Equal(1, stats.Hits);
            Assert.Equal(1, stats.Misses);
            Assert.Equal(50.0, stats.HitRate); // 1 hit / 2 total = 50%
        }

        [Theory]
        [InlineData(null, null, "discovery:models:all")]
        [InlineData("chat", null, "discovery:models:capability:chat")]
        [InlineData("vision", null, "discovery:models:capability:vision")]
        [InlineData(null, 123, "discovery:models:virtualkey:123")]
        [InlineData("chat", 456, "discovery:models:virtualkey:456:capability:chat")]
        public void BuildCacheKey_Should_Generate_Correct_Keys(string capability, int? virtualKeyId, string expected)
        {
            // Act
            var actual = DiscoveryCacheService.BuildCacheKey(capability, virtualKeyId);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task Service_Should_Work_Without_Redis()
        {
            // Arrange - Create service without distributed cache
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(x => x.GetService(typeof(IDistributedCache)))
                .Returns((IDistributedCache)null);

            var service = new DiscoveryCacheService(
                Options.Create(_options),
                _mockMemoryCache.Object,
                _mockLogger.Object,
                serviceProvider.Object);

            var cacheKey = "test-key";
            var results = new DiscoveryModelsResult { Data = new List<object>(), Count = 0 };

            var memoryCacheEntry = new Mock<Microsoft.Extensions.Caching.Memory.ICacheEntry>();
            memoryCacheEntry.SetupAllProperties();
            _mockMemoryCache
                .Setup(x => x.CreateEntry(cacheKey))
                .Returns(memoryCacheEntry.Object);

            // Act
            await service.SetDiscoveryResultsAsync(cacheKey, results);

            // Assert - Should still cache in memory
            _mockMemoryCache.Verify(x => x.CreateEntry(cacheKey), Times.Once);
            _mockDistributedCache.Verify(
                x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), 
                Times.Never);
        }

        private DiscoveryCacheService CreateServiceWithOptions(DiscoveryCacheOptions options)
        {
            var mockOptions = new Mock<IOptions<DiscoveryCacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(options);

            return new DiscoveryCacheService(
                mockOptions.Object,
                _mockMemoryCache.Object,
                _mockLogger.Object,
                _mockServiceProvider.Object);
        }
    }
}