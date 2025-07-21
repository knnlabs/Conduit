using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class RedisCacheStatisticsCollectorTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ISubscriber> _mockSubscriber;
        private readonly Mock<ILogger<RedisCacheStatisticsCollector>> _mockLogger;
        private readonly RedisCacheStatisticsCollector _collector;

        public RedisCacheStatisticsCollectorTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockLogger = new Mock<ILogger<RedisCacheStatisticsCollector>>();

            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(_mockSubscriber.Object);

            _collector = new RedisCacheStatisticsCollector(_mockRedis.Object, _mockLogger.Object, "test-instance");
        }

        [Fact]
        public async Task RecordOperationAsync_HitOperation_IncrementsCounters()
        {
            // Arrange
            var operation = new CacheOperation
            {
                Region = CacheRegion.ProviderResponses,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };

            // Act
            await _collector.RecordOperationAsync(operation);

            // Assert
            _mockDatabase.Verify(db => db.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("ApiResponses") && k.ToString().Contains("test-instance")),
                "HitCount",
                1,
                CommandFlags.None), Times.Once);

            _mockDatabase.Verify(db => db.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("ApiResponses") && k.ToString().Contains("global")),
                "HitCount",
                1,
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task RecordOperationAsync_MissOperation_IncrementsCounters()
        {
            // Arrange
            var operation = new CacheOperation
            {
                Region = CacheRegion.ModelMetadata,
                OperationType = CacheOperationType.Miss,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(5)
            };

            // Act
            await _collector.RecordOperationAsync(operation);

            // Assert
            _mockDatabase.Verify(db => db.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("ModelCapabilities") && k.ToString().Contains("test-instance")),
                "MissCount",
                1,
                CommandFlags.None), Times.Once);

            _mockDatabase.Verify(db => db.HashIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("ModelCapabilities") && k.ToString().Contains("global")),
                "MissCount",
                1,
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task RecordOperationAsync_StoresResponseTimes()
        {
            // Arrange
            var operation = new CacheOperation
            {
                Region = CacheRegion.ProviderHealth,
                OperationType = CacheOperationType.Get,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(25)
            };

            // Act
            await _collector.RecordOperationAsync(operation);

            // Assert
            _mockDatabase.Verify(db => db.SortedSetAddAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("response") && k.ToString().Contains("Get")),
                It.IsAny<RedisValue>(),
                25.0,
                When.Always,
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task RecordOperationAsync_ErrorOperation_IncrementsErrorCount()
        {
            // Arrange
            var operation = new CacheOperation
            {
                Region = CacheRegion.VirtualKeys,
                OperationType = CacheOperationType.Set,
                Success = false,
                Duration = TimeSpan.FromMilliseconds(15)
            };

            // Act
            await _collector.RecordOperationAsync(operation);

            // Assert
            _mockDatabase.Verify(db => db.HashIncrementAsync(
                It.IsAny<RedisKey>(),
                "ErrorCount",
                1,
                CommandFlags.None), Times.Exactly(2)); // Once for instance, once for global
        }

        [Fact]
        public async Task GetAggregatedStatisticsAsync_ReturnsGlobalStatistics()
        {
            // Arrange
            var region = CacheRegion.ProviderResponses;
            var hashEntries = new HashEntry[]
            {
                new HashEntry("HitCount", 100),
                new HashEntry("MissCount", 20),
                new HashEntry("SetCount", 50),
                new HashEntry("ErrorCount", 2)
            };

            _mockDatabase.Setup(db => db.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("global")),
                CommandFlags.None))
                .ReturnsAsync(hashEntries);

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(new RedisValue[] { "test-instance" });

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            // Act
            var stats = await _collector.GetAggregatedStatisticsAsync(region);

            // Assert
            Assert.Equal(100, stats.HitCount);
            Assert.Equal(20, stats.MissCount);
            Assert.Equal(50, stats.SetCount);
            Assert.Equal(2, stats.ErrorCount);
            Assert.Equal(83.33, Math.Round(stats.HitRate, 2));
        }

        [Fact]
        public async Task GetActiveInstancesAsync_ReturnsOnlyActiveInstances()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var activeTimestamp = now.ToUnixTimeMilliseconds().ToString();
            var expiredTimestamp = now.AddMinutes(-5).ToUnixTimeMilliseconds().ToString();

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(new RedisValue[] { "instance1", "instance2", "instance3" });

            _mockDatabase.SetupSequence(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(activeTimestamp)    // instance1 - active
                .ReturnsAsync(expiredTimestamp)    // instance2 - expired
                .ReturnsAsync(activeTimestamp);    // instance3 - active

            // Act
            var activeInstances = (await _collector.GetActiveInstancesAsync()).ToList();

            // Assert
            Assert.Equal(2, activeInstances.Count);
            Assert.Contains("instance1", activeInstances);
            Assert.Contains("instance3", activeInstances);
            Assert.DoesNotContain("instance2", activeInstances);
        }

        [Fact]
        public async Task RegisterInstanceAsync_AddsInstanceToSet()
        {
            // Act
            await _collector.RegisterInstanceAsync();

            // Assert
            _mockDatabase.Verify(db => db.SetAddAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("instances")),
                "test-instance",
                CommandFlags.None), Times.Once);

            _mockDatabase.Verify(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("heartbeat") && k.ToString().Contains("test-instance")),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                When.Always,
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task UnregisterInstanceAsync_RemovesInstanceFromSet()
        {
            // Act
            await _collector.UnregisterInstanceAsync();

            // Assert
            _mockDatabase.Verify(db => db.SetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("instances")),
                "test-instance",
                CommandFlags.None), Times.Once);

            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("heartbeat") && k.ToString().Contains("test-instance")),
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task ResetStatisticsAsync_DeletesInstanceKeys()
        {
            // Arrange
            var region = CacheRegion.ProviderResponses;

            // Act
            await _collector.ResetStatisticsAsync(region);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("ApiResponses") && k.ToString().Contains("test-instance")),
                CommandFlags.None), Times.Once);

            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("response") && k.ToString().Contains("Get")),
                CommandFlags.None), Times.Once);

            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("response") && k.ToString().Contains("Set")),
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task ExportStatisticsAsync_PrometheusFormat_ReturnsCorrectFormat()
        {
            // Arrange
            var hashEntries = new HashEntry[]
            {
                new HashEntry("HitCount", 1000),
                new HashEntry("MissCount", 100),
                new HashEntry("SetCount", 500)
            };

            _mockDatabase.Setup(db => db.HashGetAllAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(hashEntries);

            _mockDatabase.Setup(db => db.SetMembersAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(new RedisValue[] { "test-instance" });

            _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
                .ReturnsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            // Act
            var export = await _collector.ExportStatisticsAsync("prometheus");

            // Assert
            Assert.Contains("# HELP cache_hits_total", export);
            Assert.Contains("# TYPE cache_hits_total counter", export);
            Assert.Contains("cache_hits_total{region=", export);
            Assert.Contains("cache_misses_total{region=", export);
            Assert.Contains("cache_hit_rate{region=", export);
        }

        [Fact]
        public async Task ConfigureAlertsAsync_StoresThresholdsInRedis()
        {
            // Arrange
            var region = CacheRegion.ProviderResponses;
            var thresholds = new CacheAlertThresholds
            {
                MinHitRate = 80.0,
                MaxResponseTime = TimeSpan.FromMilliseconds(100),
                MaxErrorRate = 5.0
            };

            // Act
            await _collector.ConfigureAlertsAsync(region, thresholds);

            // Assert
            _mockDatabase.Verify(db => db.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("alerts") && k.ToString().Contains("ApiResponses")),
                "thresholds",
                It.IsAny<RedisValue>(),
                When.Always,
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public void Dispose_UnregistersInstance()
        {
            // Act
            _collector.Dispose();

            // Assert
            _mockDatabase.Verify(db => db.SetRemoveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                CommandFlags.None), Times.Once);
        }
    }
}