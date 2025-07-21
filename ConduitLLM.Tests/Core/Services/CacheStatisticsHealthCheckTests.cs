using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class CacheStatisticsHealthCheckTests
    {
        private readonly Mock<ICacheStatisticsCollector> _mockStatsCollector;
        private readonly Mock<IDistributedCacheStatisticsCollector> _mockDistributedCollector;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<ILogger<CacheStatisticsHealthCheck>> _mockLogger;
        private readonly CacheStatisticsHealthCheck _healthCheck;

        public CacheStatisticsHealthCheckTests()
        {
            _mockDistributedCollector = new Mock<IDistributedCacheStatisticsCollector>();
            _mockStatsCollector = _mockDistributedCollector.As<ICacheStatisticsCollector>();
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockLogger = new Mock<ILogger<CacheStatisticsHealthCheck>>();

            var thresholds = Options.Create(new StatisticsAlertThresholds
            {
                MaxInstanceMissingTime = TimeSpan.FromMinutes(1),
                MaxAggregationLatency = TimeSpan.FromMilliseconds(500),
                MaxDriftPercentage = 5.0,
                MaxRedisMemoryBytes = 1024 * 1024 * 1024, // 1GB
                MaxRecordingLatencyP99Ms = 10.0,
                MinActiveInstances = 1
            });

            _healthCheck = new CacheStatisticsHealthCheck(
                _mockStatsCollector.Object,
                _mockRedis.Object,
                _mockLogger.Object,
                thresholds);
        }

        [Fact]
        public async Task CheckHealthAsync_AllSystemsHealthy_ReturnsHealthy()
        {
            // Arrange
            _mockRedis.Setup(r => r.IsConnected).Returns(true);
            
            var mockDatabase = new Mock<IDatabase>();
            mockDatabase.Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(TimeSpan.FromMilliseconds(10));
            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);

            _mockDistributedCollector.Setup(c => c.GetActiveInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "instance-1", "instance-2" });

            _mockDistributedCollector.Setup(c => c.GetAggregatedStatisticsAsync(It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CacheStatistics { HitCount = 100, MissCount = 20 });

            var mockServer = new Mock<IServer>();
            mockServer.Setup(s => s.InfoAsync(It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new IGrouping<string, KeyValuePair<string, string>>[]
                {
                    new MockGrouping<string, KeyValuePair<string, string>>("Memory", new[]
                    {
                        new KeyValuePair<string, string>("used_memory", "524288000") // 500MB
                    })
                });
            
            var endPoint = new System.Net.DnsEndPoint("localhost", 6379);
            _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(new[] { endPoint });
            _mockRedis.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
                .Returns(mockServer.Object);

            // Act
            var result = await _healthCheck.CheckHealthAsync();

            // Assert
            result.Status.Should().Be(ConduitLLM.Core.Interfaces.HealthStatus.Healthy);
            result.RedisConnected.Should().BeTrue();
            result.ActiveInstances.Should().Be(2);
            result.MissingInstances.Should().Be(0);
            result.Messages.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckHealthAsync_RedisDisconnected_ReturnsUnhealthy()
        {
            // Arrange
            _mockRedis.Setup(r => r.IsConnected).Returns(false);
            _mockDistributedCollector.Setup(c => c.GetActiveInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "instance-1" });

            // Act
            var result = await _healthCheck.CheckHealthAsync();

            // Assert
            result.Status.Should().Be(ConduitLLM.Core.Interfaces.HealthStatus.Unhealthy);
            result.RedisConnected.Should().BeFalse();
            result.Messages.Should().Contain("Redis connection is unhealthy");
        }

        [Fact]
        public async Task CheckHealthAsync_NoActiveInstances_ReturnsDegraded()
        {
            // Arrange
            SetupHealthyRedis();
            _mockDistributedCollector.Setup(c => c.GetActiveInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<string>());

            // Act
            var result = await _healthCheck.CheckHealthAsync();

            // Assert
            result.Status.Should().Be(ConduitLLM.Core.Interfaces.HealthStatus.Degraded);
            result.ActiveInstances.Should().Be(0);
            result.Messages.Should().Contain(m => m.Contains("Active instances (0) below minimum threshold"));
        }

        [Fact]
        public async Task ValidateAccuracyAsync_ConsistentStatistics_ReturnsAccurate()
        {
            // Arrange
            var aggregatedStats = new CacheStatistics
            {
                Region = CacheRegion.ProviderResponses,
                HitCount = 300,
                MissCount = 100
            };

            var perInstanceStats = new Dictionary<string, CacheStatistics>
            {
                ["instance-1"] = new CacheStatistics { HitCount = 150, MissCount = 50 },
                ["instance-2"] = new CacheStatistics { HitCount = 150, MissCount = 50 }
            };

            _mockDistributedCollector.Setup(c => c.GetAggregatedStatisticsAsync(It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(aggregatedStats);

            _mockDistributedCollector.Setup(c => c.GetPerInstanceStatisticsAsync(It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(perInstanceStats);

            // Act
            var report = await _healthCheck.ValidateAccuracyAsync();

            // Assert
            report.IsAccurate.Should().BeTrue();
            report.Discrepancies.Should().BeEmpty();
            report.MaxDriftPercentage.Should().Be(0);
        }

        [Fact]
        public async Task ValidateAccuracyAsync_StatisticsDrift_ReportsDiscrepancy()
        {
            // Arrange
            var aggregatedStats = new CacheStatistics
            {
                Region = CacheRegion.ProviderResponses,
                HitCount = 300,
                MissCount = 100
            };

            var perInstanceStats = new Dictionary<string, CacheStatistics>
            {
                ["instance-1"] = new CacheStatistics { HitCount = 200, MissCount = 50 },
                ["instance-2"] = new CacheStatistics { HitCount = 150, MissCount = 50 }
            };

            _mockDistributedCollector.Setup(c => c.GetAggregatedStatisticsAsync(It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(aggregatedStats);

            _mockDistributedCollector.Setup(c => c.GetPerInstanceStatisticsAsync(It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(perInstanceStats);

            // Act
            var report = await _healthCheck.ValidateAccuracyAsync();

            // Assert
            report.IsAccurate.Should().BeFalse();
            report.Discrepancies.Should().NotBeEmpty();
            report.Discrepancies.First().Type.Should().Be(DiscrepancyType.CountMismatch);
            report.Discrepancies.First().ExpectedValue.Should().Be(350); // 200 + 150
            report.Discrepancies.First().ActualValue.Should().Be(300);
        }

        [Fact]
        public async Task GetPerformanceMetricsAsync_ReturnsMetrics()
        {
            // Arrange
            _mockDistributedCollector.Setup(c => c.GetActiveInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "instance-1", "instance-2" });

            _mockDistributedCollector.Setup(c => c.GetAggregatedStatisticsAsync(It.IsAny<CacheRegion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CacheStatistics 
                { 
                    AverageGetTime = TimeSpan.FromMilliseconds(5),
                    MemoryUsageBytes = 1024 * 1024 // 1MB
                });

            SetupHealthyRedis();

            // Act
            var metrics = await _healthCheck.GetPerformanceMetricsAsync();

            // Assert
            metrics.ActiveInstances.Should().Be(2);
            metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            metrics.RegionMetrics.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AlertTriggered_HighRedisMemory_RaisesEvent()
        {
            // Arrange
            var alertReceived = false;
            StatisticsMonitoringAlert? receivedAlert = null;

            _healthCheck.AlertTriggered += (sender, args) =>
            {
                alertReceived = true;
                receivedAlert = args.Alert;
            };

            SetupHealthyRedis(memoryUsageBytes: 2L * 1024 * 1024 * 1024); // 2GB

            _mockDistributedCollector.Setup(c => c.GetActiveInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "instance-1" });

            // Act
            await _healthCheck.CheckHealthAsync();
            await Task.Delay(100); // Allow event processing

            // Assert
            alertReceived.Should().BeTrue();
            receivedAlert.Should().NotBeNull();
            receivedAlert!.Type.Should().Be(StatisticsAlertType.HighRedisMemory);
            receivedAlert.Severity.Should().Be(AlertSeverity.Warning);
        }

        [Fact]
        public async Task ConfigureAlertingAsync_UpdatesThresholds()
        {
            // Arrange
            var newThresholds = new StatisticsAlertThresholds
            {
                MaxAggregationLatency = TimeSpan.FromMilliseconds(200),
                MaxDriftPercentage = 10.0,
                MinActiveInstances = 2
            };

            // Act
            await _healthCheck.ConfigureAlertingAsync(newThresholds);

            // Configure health check to trigger based on new thresholds
            _mockDistributedCollector.Setup(c => c.GetActiveInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "instance-1" }); // Only 1 instance

            var result = await _healthCheck.CheckHealthAsync();

            // Assert
            result.Status.Should().Be(ConduitLLM.Core.Interfaces.HealthStatus.Degraded);
            result.Messages.Should().Contain(m => m.Contains("Active instances (1) below minimum threshold (2)"));
        }

        private void SetupHealthyRedis(long memoryUsageBytes = 524288000)
        {
            _mockRedis.Setup(r => r.IsConnected).Returns(true);
            
            var mockDatabase = new Mock<IDatabase>();
            mockDatabase.Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(TimeSpan.FromMilliseconds(10));
            _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);

            var mockServer = new Mock<IServer>();
            mockServer.Setup(s => s.InfoAsync(It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new IGrouping<string, KeyValuePair<string, string>>[]
                {
                    new MockGrouping<string, KeyValuePair<string, string>>("Memory", new[]
                    {
                        new KeyValuePair<string, string>("used_memory", memoryUsageBytes.ToString())
                    })
                });
            
            var endPoint = new System.Net.DnsEndPoint("localhost", 6379);
            _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(new[] { endPoint });
            _mockRedis.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
                .Returns(mockServer.Object);
        }

        private class MockGrouping<TKey, TElement> : IGrouping<TKey, TElement>
        {
            private readonly TKey _key;
            private readonly IEnumerable<TElement> _elements;

            public MockGrouping(TKey key, IEnumerable<TElement> elements)
            {
                _key = key;
                _elements = elements;
            }

            public TKey Key => _key;

            public IEnumerator<TElement> GetEnumerator() => _elements.GetEnumerator();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}