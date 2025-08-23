using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class CacheStatisticsCollectorTests
    {
        private readonly Mock<ILogger<CacheStatisticsCollector>> _mockLogger;
        private readonly Mock<IOptions<CacheStatisticsOptions>> _mockOptions;
        private readonly Mock<ICacheStatisticsStore> _mockStore;
        private readonly CacheStatisticsOptions _options;
        
        public CacheStatisticsCollectorTests()
        {
            _mockLogger = new Mock<ILogger<CacheStatisticsCollector>>();
            _mockOptions = new Mock<IOptions<CacheStatisticsOptions>>();
            _mockStore = new Mock<ICacheStatisticsStore>();
            
            _options = new CacheStatisticsOptions
            {
                AggregationInterval = TimeSpan.FromMilliseconds(100),
                PersistenceInterval = TimeSpan.FromMilliseconds(100)
            };
            _mockOptions.Setup(x => x.Value).Returns(_options);
        }

        [Fact]
        public async Task RecordOperationAsync_Hit_UpdatesStatisticsCorrectly()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var operation = new CacheOperation
            {
                Region = CacheRegion.VirtualKeys,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10),
                Key = "test-key"
            };

            // Act
            await collector.RecordOperationAsync(operation);
            var stats = await collector.GetStatisticsAsync(CacheRegion.VirtualKeys);

            // Assert
            Assert.Equal(1, stats.HitCount);
            Assert.Equal(0, stats.MissCount);
            Assert.Equal(100.0, stats.HitRate);
        }

        [Fact]
        public async Task RecordOperationAsync_Miss_UpdatesStatisticsCorrectly()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var operation = new CacheOperation
            {
                Region = CacheRegion.VirtualKeys,
                OperationType = CacheOperationType.Miss,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(5)
            };

            // Act
            await collector.RecordOperationAsync(operation);
            var stats = await collector.GetStatisticsAsync(CacheRegion.VirtualKeys);

            // Assert
            Assert.Equal(0, stats.HitCount);
            Assert.Equal(1, stats.MissCount);
            Assert.Equal(0.0, stats.HitRate);
        }

        [Fact]
        public async Task RecordOperationAsync_MultipleMixedOperations_CalculatesHitRateCorrectly()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            
            // Act - 3 hits, 2 misses
            await collector.RecordOperationAsync(new CacheOperation 
            { 
                Region = CacheRegion.RateLimits, 
                OperationType = CacheOperationType.Hit, 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(5) 
            });
            await collector.RecordOperationAsync(new CacheOperation 
            { 
                Region = CacheRegion.RateLimits, 
                OperationType = CacheOperationType.Miss, 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(5) 
            });
            await collector.RecordOperationAsync(new CacheOperation 
            { 
                Region = CacheRegion.RateLimits, 
                OperationType = CacheOperationType.Hit, 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(5) 
            });
            await collector.RecordOperationAsync(new CacheOperation 
            { 
                Region = CacheRegion.RateLimits, 
                OperationType = CacheOperationType.Miss, 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(5) 
            });
            await collector.RecordOperationAsync(new CacheOperation 
            { 
                Region = CacheRegion.RateLimits, 
                OperationType = CacheOperationType.Hit, 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(5) 
            });

            var stats = await collector.GetStatisticsAsync(CacheRegion.RateLimits);

            // Assert
            Assert.Equal(3, stats.HitCount);
            Assert.Equal(2, stats.MissCount);
            Assert.Equal(60.0, stats.HitRate); // 3/5 = 60%
        }

        [Fact]
        public async Task RecordOperationAsync_SetOperation_UpdatesSetCount()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var operation = new CacheOperation
            {
                Region = CacheRegion.ModelMetadata,
                OperationType = CacheOperationType.Set,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(15),
                DataSizeBytes = 1024
            };

            // Act
            await collector.RecordOperationAsync(operation);
            var stats = await collector.GetStatisticsAsync(CacheRegion.ModelMetadata);

            // Assert
            Assert.Equal(1, stats.SetCount);
        }

        [Fact]
        public async Task RecordOperationAsync_ErrorOperation_UpdatesErrorCount()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var operation = new CacheOperation
            {
                Region = CacheRegion.AuthTokens,
                OperationType = CacheOperationType.Get,
                Success = false,
                Duration = TimeSpan.FromMilliseconds(20)
            };

            // Act
            await collector.RecordOperationAsync(operation);
            var stats = await collector.GetStatisticsAsync(CacheRegion.AuthTokens);

            // Assert
            Assert.Equal(1, stats.ErrorCount);
        }

        [Fact]
        public async Task RecordOperationBatchAsync_MultipleOperations_RecordsAll()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var operations = new[]
            {
                new CacheOperation { Region = CacheRegion.Default, OperationType = CacheOperationType.Hit, Success = true, Duration = TimeSpan.FromMilliseconds(5) },
                new CacheOperation { Region = CacheRegion.Default, OperationType = CacheOperationType.Miss, Success = true, Duration = TimeSpan.FromMilliseconds(5) },
                new CacheOperation { Region = CacheRegion.Default, OperationType = CacheOperationType.Set, Success = true, Duration = TimeSpan.FromMilliseconds(10) }
            };

            // Act
            await collector.RecordOperationBatchAsync(operations);
            var stats = await collector.GetStatisticsAsync(CacheRegion.Default);

            // Assert
            Assert.Equal(1, stats.HitCount);
            Assert.Equal(1, stats.MissCount);
            Assert.Equal(1, stats.SetCount);
        }

        [Fact]
        public async Task GetAllStatisticsAsync_ReturnsAllRegionStatistics()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            
            // Record operations in different regions
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.VirtualKeys, OperationType = CacheOperationType.Hit, Success = true, Duration = TimeSpan.FromMilliseconds(5) });
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.RateLimits, OperationType = CacheOperationType.Miss, Success = true, Duration = TimeSpan.FromMilliseconds(5) });

            // Act
            var allStats = await collector.GetAllStatisticsAsync();

            // Assert
            Assert.NotEmpty(allStats);
            Assert.Equal(1, allStats[CacheRegion.VirtualKeys].HitCount);
            Assert.Equal(1, allStats[CacheRegion.RateLimits].MissCount);
        }

        [Fact]
        public async Task ResetStatisticsAsync_ClearsRegionStatistics()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            
            // Record some operations
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.GlobalSettings, OperationType = CacheOperationType.Hit, Success = true, Duration = TimeSpan.FromMilliseconds(5) });
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.GlobalSettings, OperationType = CacheOperationType.Miss, Success = true, Duration = TimeSpan.FromMilliseconds(5) });

            // Act
            await collector.ResetStatisticsAsync(CacheRegion.GlobalSettings);
            var stats = await collector.GetStatisticsAsync(CacheRegion.GlobalSettings);

            // Assert
            Assert.Equal(0, stats.HitCount);
            Assert.Equal(0, stats.MissCount);
        }

        [Fact]
        public async Task ExportStatisticsAsync_PrometheusFormat_ReturnsCorrectFormat()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.VirtualKeys, OperationType = CacheOperationType.Hit, Success = true, Duration = TimeSpan.FromMilliseconds(10) });
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.VirtualKeys, OperationType = CacheOperationType.Miss, Success = true, Duration = TimeSpan.FromMilliseconds(5) });

            // Act
            var prometheusData = await collector.ExportStatisticsAsync("prometheus");

            // Assert
            Assert.Contains("conduit_cache_hits_total", prometheusData);
            Assert.Contains("conduit_cache_misses_total", prometheusData);
            Assert.Contains("conduit_cache_hit_rate", prometheusData);
            Assert.Contains("region=\"virtualkeys\"", prometheusData);
        }

        [Fact]
        public async Task ExportStatisticsAsync_JsonFormat_ReturnsValidJson()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            await collector.RecordOperationAsync(new CacheOperation { Region = CacheRegion.VirtualKeys, OperationType = CacheOperationType.Hit, Success = true, Duration = TimeSpan.FromMilliseconds(10) });

            // Act
            var jsonData = await collector.ExportStatisticsAsync("json");

            // Assert
            Assert.NotNull(jsonData);
            Assert.Contains("\"VirtualKeys\"", jsonData);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
            Assert.NotNull(deserialized);
        }

        [Fact]
        public async Task ConfigureAlertsAsync_ConfiguresThresholds()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var thresholds = new CacheAlertThresholds
            {
                MinHitRate = 80.0,
                MaxResponseTime = TimeSpan.FromMilliseconds(100),
                Enabled = true
            };

            // Act
            await collector.ConfigureAlertsAsync(CacheRegion.VirtualKeys, thresholds);
            
            // Record operations that should trigger alert
            for (int i = 0; i < 10; i++)
            {
                await collector.RecordOperationAsync(new CacheOperation 
                { 
                    Region = CacheRegion.VirtualKeys, 
                    OperationType = i < 7 ? CacheOperationType.Miss : CacheOperationType.Hit, // 30% hit rate
                    Success = true, 
                    Duration = TimeSpan.FromMilliseconds(10) 
                });
            }

            var alerts = await collector.GetActiveAlertsAsync();

            // Assert
            Assert.NotEmpty(alerts);
            Assert.Contains(alerts, a => a.AlertType == CacheAlertType.LowHitRate);
        }

        [Fact]
        public async Task StatisticsUpdatedEvent_FiresOnOperationRecord()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);
            var eventFired = false;
            CacheStatisticsUpdatedEventArgs? eventArgs = null;
            
            collector.StatisticsUpdated += (sender, args) =>
            {
                eventFired = true;
                eventArgs = args;
            };

            var operation = new CacheOperation
            {
                Region = CacheRegion.VirtualKeys,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };

            // Act
            await collector.RecordOperationAsync(operation);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(eventArgs);
            Assert.Equal(CacheRegion.VirtualKeys, eventArgs.Region);
            Assert.Equal(operation, eventArgs.TriggeringOperation);
        }

        [Fact]
        public async Task GetStatisticsForWindowAsync_WithStore_ReturnsWindowedStats()
        {
            // Arrange
            var expectedStats = new CacheStatistics
            {
                Region = CacheRegion.VirtualKeys,
                HitCount = 100,
                MissCount = 20
            };

            _mockStore.Setup(x => x.GetStatisticsForWindowAsync(
                    It.IsAny<CacheRegion>(), 
                    It.IsAny<DateTime>(), 
                    It.IsAny<DateTime>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStats);

            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object, _mockStore.Object);

            // Act
            var stats = await collector.GetStatisticsForWindowAsync(CacheRegion.VirtualKeys, TimeWindow.LastHour);

            // Assert
            Assert.Equal(100, stats.HitCount);
            Assert.Equal(20, stats.MissCount);
            _mockStore.Verify(x => x.GetStatisticsForWindowAsync(
                CacheRegion.VirtualKeys, 
                It.IsAny<DateTime>(), 
                It.IsAny<DateTime>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetHistoricalStatisticsAsync_WithStore_ReturnsTimeSeriesData()
        {
            // Arrange
            var expectedData = new List<TimeSeriesStatistics>
            {
                new TimeSeriesStatistics
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    Statistics = new CacheStatistics { Region = CacheRegion.VirtualKeys, HitCount = 50 },
                    Interval = TimeSpan.FromMinutes(5)
                },
                new TimeSeriesStatistics
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    Statistics = new CacheStatistics { Region = CacheRegion.VirtualKeys, HitCount = 75 },
                    Interval = TimeSpan.FromMinutes(5)
                }
            };

            _mockStore.Setup(x => x.GetTimeSeriesStatisticsAsync(
                    It.IsAny<CacheRegion>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedData);

            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object, _mockStore.Object);

            // Act
            var timeSeries = await collector.GetHistoricalStatisticsAsync(
                CacheRegion.VirtualKeys,
                DateTime.UtcNow.AddMinutes(-15),
                DateTime.UtcNow,
                TimeSpan.FromMinutes(5));

            // Assert
            Assert.Equal(2, timeSeries.Count());
            _mockStore.Verify(x => x.GetTimeSeriesStatisticsAsync(
                CacheRegion.VirtualKeys,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                TimeSpan.FromMinutes(5),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Dispose_DisposesTimersCleanly()
        {
            // Arrange
            var collector = new CacheStatisticsCollector(_mockLogger.Object, _mockOptions.Object);

            // Act & Assert - Should not throw
            collector.Dispose();
        }
    }
}