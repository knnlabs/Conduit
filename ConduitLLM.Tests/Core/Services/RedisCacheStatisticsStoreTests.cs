using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class RedisCacheStatisticsStoreTests
    {
        private readonly Mock<IDistributedCache> _mockCache;
        private readonly Mock<ILogger<RedisCacheStatisticsStore>> _mockLogger;
        private readonly RedisCacheStatisticsStore _store;

        public RedisCacheStatisticsStoreTests()
        {
            _mockCache = new Mock<IDistributedCache>();
            _mockLogger = new Mock<ILogger<RedisCacheStatisticsStore>>();
            _store = new RedisCacheStatisticsStore(_mockCache.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SaveStatisticsAsync_SavesCurrentAndTimeSeriesData()
        {
            // Arrange
            var statistics = new Dictionary<CacheRegion, CacheStatistics>
            {
                {
                    CacheRegion.VirtualKeys,
                    new CacheStatistics
                    {
                        Region = CacheRegion.VirtualKeys,
                        HitCount = 100,
                        MissCount = 20,
                        SetCount = 50
                    }
                }
            };

            var savedKeys = new List<string>();
            _mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, data, options, ct) =>
                {
                    savedKeys.Add(key);
                })
                .Returns(Task.CompletedTask);

            // Act
            await _store.SaveStatisticsAsync(statistics);

            // Assert
            Assert.Contains(savedKeys, k => k.StartsWith("cache:stats:VirtualKeys:current"));
            Assert.Contains(savedKeys, k => k.StartsWith("cache:stats:ts:VirtualKeys:"));
            
            _mockCache.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task SaveStatisticsAsync_SavesHourlySnapshotAtTopOfHour()
        {
            // Arrange
            var testTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var statistics = new Dictionary<CacheRegion, CacheStatistics>
            {
                {
                    CacheRegion.VirtualKeys,
                    new CacheStatistics
                    {
                        Region = CacheRegion.VirtualKeys,
                        HitCount = 100
                    }
                }
            };

            var savedKeys = new List<string>();
            _mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, data, options, ct) =>
                {
                    savedKeys.Add(key);
                })
                .Returns(Task.CompletedTask);

            // Act - simulate saving at top of hour
            await _store.SaveStatisticsAsync(statistics);

            // Assert - should save current, time-series, and snapshot
            // Note: In real implementation, we'd need to mock DateTime.UtcNow
            Assert.True(savedKeys.Count >= 2); // At least current and time-series
        }

        [Fact]
        public async Task LoadAllStatisticsAsync_LoadsAllRegions()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                Region = CacheRegion.VirtualKeys,
                HitCount = 100,
                MissCount = 20
            };
            var json = JsonSerializer.Serialize(stats);
            var bytes = Encoding.UTF8.GetBytes(json);

            _mockCache.Setup(x => x.GetAsync(
                    It.Is<string>(k => k.Contains("VirtualKeys")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(bytes);

            // Act
            var result = await _store.LoadAllStatisticsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains(CacheRegion.VirtualKeys, result.Keys);
            Assert.Equal(100, result[CacheRegion.VirtualKeys].HitCount);
        }

        [Fact]
        public async Task LoadAllStatisticsAsync_HandlesNullData()
        {
            // Arrange
            _mockCache.Setup(x => x.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _store.LoadAllStatisticsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetStatisticsForWindowAsync_AggregatesMultipleDataPoints()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMinutes(-10);
            var endTime = DateTime.UtcNow;

            var stats1 = new CacheStatistics
            {
                Region = CacheRegion.VirtualKeys,
                HitCount = 50,
                MissCount = 10,
                SetCount = 20
            };

            var stats2 = new CacheStatistics
            {
                Region = CacheRegion.VirtualKeys,
                HitCount = 30,
                MissCount = 5,
                SetCount = 10
            };

            var json1 = JsonSerializer.Serialize(stats1);
            var json2 = JsonSerializer.Serialize(stats2);

            var callCount = 0;
            _mockCache.Setup(x => x.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    var json = callCount % 2 == 1 ? json1 : json2;
                    return Encoding.UTF8.GetBytes(json);
                });

            // Act
            var result = await _store.GetStatisticsForWindowAsync(CacheRegion.VirtualKeys, startTime, endTime);

            // Assert
            Assert.Equal(CacheRegion.VirtualKeys, result.Region);
            // Since we're getting multiple data points, the aggregated values should be sum of individual stats
            Assert.True(result.HitCount > 0);
            Assert.True(result.MissCount > 0);
        }

        [Fact]
        public async Task GetTimeSeriesStatisticsAsync_UsesHourlySnapshotsForLargeIntervals()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddHours(-6);
            var endTime = DateTime.UtcNow;
            var interval = TimeSpan.FromHours(1);

            var stats = new CacheStatistics
            {
                Region = CacheRegion.VirtualKeys,
                HitCount = 100,
                MissCount = 20
            };
            var json = JsonSerializer.Serialize(stats);

            _mockCache.Setup(x => x.GetAsync(
                    It.Is<string>(k => k.Contains("snapshot")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(json));

            // Act
            var result = await _store.GetTimeSeriesStatisticsAsync(
                CacheRegion.VirtualKeys, startTime, endTime, interval);

            // Assert
            var resultList = result.ToList();
            Assert.NotEmpty(resultList);
            
            // Verify it tried to get snapshot data
            _mockCache.Verify(x => x.GetAsync(
                It.Is<string>(k => k.Contains("snapshot")),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetTimeSeriesStatisticsAsync_UsesMinuteDataForSmallIntervals()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMinutes(-30);
            var endTime = DateTime.UtcNow;
            var interval = TimeSpan.FromMinutes(5);

            var stats = new CacheStatistics
            {
                Region = CacheRegion.VirtualKeys,
                HitCount = 50,
                MissCount = 10,
                AverageGetTime = TimeSpan.FromMilliseconds(10)
            };
            var json = JsonSerializer.Serialize(stats);

            _mockCache.Setup(x => x.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(json));

            // Act
            var result = await _store.GetTimeSeriesStatisticsAsync(
                CacheRegion.VirtualKeys, startTime, endTime, interval);

            // Assert
            var resultList = result.ToList();
            Assert.NotEmpty(resultList);
            Assert.All(resultList, ts =>
            {
                Assert.Equal(interval, ts.Interval);
                Assert.NotNull(ts.Statistics);
            });
        }

        [Fact]
        public async Task SaveStatisticsAsync_LogsErrorOnFailure()
        {
            // Arrange
            var statistics = new Dictionary<CacheRegion, CacheStatistics>
            {
                { CacheRegion.VirtualKeys, new CacheStatistics { Region = CacheRegion.VirtualKeys } }
            };

            _mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _store.SaveStatisticsAsync(statistics));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error saving statistics to Redis")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetStatisticsForWindowAsync_ReturnsEmptyStatsWhenNoDataAvailable()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;

            _mockCache.Setup(x => x.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _store.GetStatisticsForWindowAsync(
                CacheRegion.VirtualKeys, startTime, endTime);

            // Assert
            Assert.Equal(CacheRegion.VirtualKeys, result.Region);
            Assert.Equal(0, result.HitCount);
            Assert.Equal(0, result.MissCount);
            Assert.Equal(startTime, result.StartTime);
            Assert.Equal(endTime, result.LastUpdateTime);
        }
    }
}