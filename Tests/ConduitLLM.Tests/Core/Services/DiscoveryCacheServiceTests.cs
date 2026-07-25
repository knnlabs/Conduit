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
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<ILogger<DiscoveryCacheService>> _mockLogger;
        private readonly DiscoveryCacheOptions _options;
        private readonly DiscoveryCacheService _service;

        public DiscoveryCacheServiceTests()
        {
            _mockCacheManager = new Mock<ICacheManager>();
            _mockLogger = new Mock<ILogger<DiscoveryCacheService>>();

            _options = new DiscoveryCacheOptions
            {
                EnableCaching = true,
                CacheDurationMinutes = 360,
                WarmCacheOnStartup = false
            };

            var mockOptions = new Mock<IOptions<DiscoveryCacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(_options);

            _service = new DiscoveryCacheService(
                mockOptions.Object,
                _mockCacheManager.Object,
                _mockLogger.Object);
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
            _mockCacheManager.Verify(x => x.GetAsync<DiscoveryModelsResult>(It.IsAny<string>(), It.IsAny<ConduitLLM.Core.Models.CacheRegion>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetDiscoveryResultsAsync_Should_Return_From_Cache_When_Available()
        {
            // Arrange
            var cacheKey = "discovery:models:all";
            var expectedResult = new DiscoveryModelsResult
            {
                Data = new List<JsonElement> { JsonSerializer.SerializeToElement(new { id = "gpt-4", provider = "openai" }) },
                Count = 1,
                CachedAt = DateTime.UtcNow
            };

            _mockCacheManager
                .Setup(x => x.GetAsync<DiscoveryModelsResult>(cacheKey, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.GetDiscoveryResultsAsync(cacheKey);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            _mockCacheManager.Verify(x => x.GetAsync<DiscoveryModelsResult>(cacheKey, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetDiscoveryResultsAsync_Should_Return_Null_When_Cache_Empty()
        {
            // Arrange
            var cacheKey = "discovery:models:all";

            _mockCacheManager
                .Setup(x => x.GetAsync<DiscoveryModelsResult>(cacheKey, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DiscoveryModelsResult)null);

            // Act
            var result = await _service.GetDiscoveryResultsAsync(cacheKey);

            // Assert
            Assert.Null(result);
            _mockCacheManager.Verify(x => x.GetAsync<DiscoveryModelsResult>(cacheKey, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetDiscoveryResultsAsync_Should_Not_Cache_When_Caching_Disabled()
        {
            // Arrange
            _options.EnableCaching = false;
            var service = CreateServiceWithOptions(_options);
            var results = new DiscoveryModelsResult { Data = new List<JsonElement> { JsonSerializer.SerializeToElement(new { id = "test" }) }, Count = 1 };

            // Act
            await service.SetDiscoveryResultsAsync("test-key", results);

            // Assert
            _mockCacheManager.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<DiscoveryModelsResult>(), It.IsAny<ConduitLLM.Core.Models.CacheRegion>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetDiscoveryResultsAsync_Should_Cache_With_Correct_TTL()
        {
            // Arrange
            var cacheKey = "discovery:models:capability:chat";
            var results = new DiscoveryModelsResult
            {
                Data = new List<JsonElement> { JsonSerializer.SerializeToElement(new { id = "gpt-4", provider = "openai" }) },
                Count = 1
            };

            // Act
            await _service.SetDiscoveryResultsAsync(cacheKey, results);

            // Assert
            _mockCacheManager.Verify(
                x => x.SetAsync(
                    cacheKey,
                    results,
                    ConduitLLM.Core.Models.CacheRegion.ModelDiscovery,
                    It.Is<TimeSpan>(ttl => ttl == TimeSpan.FromMinutes(360)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.True(results.CachedAt != default);
        }

        [Fact]
        public async Task InvalidateAllDiscoveryAsync_Should_Call_ClearRegionAsync()
        {
            // Act
            await _service.InvalidateAllDiscoveryAsync();

            // Assert
            _mockCacheManager.Verify(
                x => x.ClearRegionAsync(ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task InvalidateAllDiscoveryAsync_Should_Handle_Errors_Gracefully()
        {
            // Arrange
            _mockCacheManager
                .Setup(x => x.ClearRegionAsync(It.IsAny<ConduitLLM.Core.Models.CacheRegion>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cache error"));

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
        public async Task InvalidatePatternAsync_Should_Use_RemoveByPatternAsync()
        {
            // Arrange
            var pattern = "capability:custom";

            // Act
            await _service.InvalidatePatternAsync(pattern);

            // Assert
            _mockCacheManager.Verify(
                x => x.RemoveByPatternAsync(pattern, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()),
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
            // Wildcard patterns should call InvalidateAllDiscoveryAsync which calls ClearRegionAsync
            _mockCacheManager.Verify(
                x => x.ClearRegionAsync(ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetStatisticsAsync_Should_Return_Correct_Stats()
        {
            // Arrange
            // Simulate some cache hits and misses
            var cacheKey = "test-key";
            _mockCacheManager
                .Setup(x => x.GetAsync<DiscoveryModelsResult>(cacheKey, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DiscoveryModelsResult)null);

            // Generate a miss
            await _service.GetDiscoveryResultsAsync(cacheKey);

            // Setup for a hit
            var result = new DiscoveryModelsResult { Data = new List<JsonElement>(), Count = 0 };
            _mockCacheManager
                .Setup(x => x.GetAsync<DiscoveryModelsResult>(cacheKey, ConduitLLM.Core.Models.CacheRegion.ModelDiscovery, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

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
        [InlineData(null, null, "all")]
        [InlineData("chat", null, "capability:chat")]
        [InlineData("vision", null, "capability:vision")]
        [InlineData(null, 123, "virtualkey:123")]
        [InlineData("chat", 456, "virtualkey:456:capability:chat")]
        public void BuildCacheKey_Should_Generate_Correct_Keys(string capability, int? virtualKeyId, string expected)
        {
            // Act
            var actual = DiscoveryCacheService.BuildCacheKey(capability, virtualKeyId);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(null, null, "all:with_pricing")]
        [InlineData("chat", null, "capability:chat:with_pricing")]
        [InlineData(null, 123, "virtualkey:123:with_pricing")]
        [InlineData("chat", 456, "virtualkey:456:capability:chat:with_pricing")]
        public void BuildCacheKey_WithPricing_AppendsVariantSuffix(string capability, int? virtualKeyId, string expected)
        {
            // Act
            var actual = DiscoveryCacheService.BuildCacheKey(capability, virtualKeyId, includePricing: true);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task SetDiscoveryResultsAsync_Should_Use_CacheManager()
        {
            // Arrange
            var cacheKey = "test-key";
            var results = new DiscoveryModelsResult { Data = new List<JsonElement>(), Count = 0 };

            // Act
            await _service.SetDiscoveryResultsAsync(cacheKey, results);

            // Assert - Should call CacheManager.SetAsync
            _mockCacheManager.Verify(x => x.SetAsync(
                cacheKey,
                results,
                ConduitLLM.Core.Models.CacheRegion.ModelDiscovery,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        private DiscoveryCacheService CreateServiceWithOptions(DiscoveryCacheOptions options)
        {
            var mockOptions = new Mock<IOptions<DiscoveryCacheOptions>>();
            mockOptions.Setup(x => x.Value).Returns(options);

            return new DiscoveryCacheService(
                mockOptions.Object,
                _mockCacheManager.Object,
                _mockLogger.Object);
        }
    }
}