using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class PrometheusAudioMetricsExporterTests : IDisposable
    {
        private readonly Mock<IAudioMetricsCollector> _mockMetricsCollector;
        private readonly Mock<ILogger<PrometheusAudioMetricsExporter>> _mockLogger;
        private readonly PrometheusExporterOptions _options;
        private readonly PrometheusAudioMetricsExporter _exporter;

        public PrometheusAudioMetricsExporterTests()
        {
            _mockMetricsCollector = new Mock<IAudioMetricsCollector>();
            _mockLogger = new Mock<ILogger<PrometheusAudioMetricsExporter>>();
            _options = new PrometheusExporterOptions
            {
                ExportInterval = TimeSpan.FromSeconds(1),
                MetricsWindow = TimeSpan.FromMinutes(5),
                CacheExpiration = TimeSpan.FromSeconds(1)
            };

            _exporter = new PrometheusAudioMetricsExporter(
                _mockMetricsCollector.Object,
                _mockLogger.Object,
                Options.Create(_options));
        }

        [Fact]
        public async Task GetMetricsAsync_ReturnsPrometheusFormattedMetrics()
        {
            // Arrange
            var aggregatedMetrics = new AggregatedAudioMetrics
            {
                Period = new DateTimeRange 
                { 
                    Start = DateTime.UtcNow.AddMinutes(-5), 
                    End = DateTime.UtcNow 
                },
                Transcription = new OperationStatistics
                {
                    TotalRequests = 100,
                    SuccessfulRequests = 95,
                    FailedRequests = 5,
                    AverageDurationMs = 2500,
                    P95DurationMs = 4000,
                    P99DurationMs = 5000,
                    CacheHitRate = 0.25,
                    TotalDataBytes = 1024000
                },
                TextToSpeech = new OperationStatistics
                {
                    TotalRequests = 50,
                    SuccessfulRequests = 48,
                    FailedRequests = 2,
                    AverageDurationMs = 1500,
                    P95DurationMs = 2000,
                    P99DurationMs = 2500,
                    CacheHitRate = 0.40,
                    TotalDataBytes = 512000
                },
                Realtime = new RealtimeStatistics
                {
                    TotalSessions = 10,
                    AverageSessionDurationSeconds = 120,
                    TotalAudioMinutes = 20,
                    AverageLatencyMs = 150,
                    DisconnectReasons = new Dictionary<string, long>
                    {
                        { "client_disconnect", 5 },
                        { "timeout", 3 },
                        { "error", 2 }
                    }
                },
                ProviderStats = new Dictionary<string, ProviderStatistics>
                {
                    {
                        "openai", new ProviderStatistics
                        {
                            Provider = "openai",
                            RequestCount = 150,
                            SuccessRate = 0.96,
                            AverageLatencyMs = 2000,
                            UptimePercentage = 99.5,
                            ErrorBreakdown = new Dictionary<string, long>
                            {
                                { "rate_limit", 3 },
                                { "timeout", 3 }
                            }
                        }
                    }
                },
                Costs = new CostAnalysis
                {
                    TranscriptionCost = 1.25m,
                    TextToSpeechCost = 0.80m,
                    RealtimeCost = 1.20m,
                    TotalCost = 3.25m,
                    CachingSavings = 0.35m
                }
            };

            var snapshot = new AudioMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                ActiveTranscriptions = 5,
                ActiveTtsOperations = 3,
                ActiveRealtimeSessions = 2,
                RequestsPerSecond = 2.5,
                CurrentErrorRate = 0.04,
                ProviderHealth = new Dictionary<string, bool> { { "openai", true } },
                Resources = new SystemResources
                {
                    CpuUsagePercent = 45.5,
                    MemoryUsageMb = 2048,
                    ActiveConnections = 25,
                    CacheSizeMb = 512
                }
            };

            _mockMetricsCollector
                .Setup(m => m.GetAggregatedMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                .ReturnsAsync(aggregatedMetrics);

            _mockMetricsCollector
                .Setup(m => m.GetCurrentSnapshotAsync())
                .ReturnsAsync(snapshot);

            // Act
            await _exporter.StartAsync(CancellationToken.None);
            var metrics = await _exporter.GetMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.NotEmpty(metrics);

            // Check for key metric names
            Assert.Contains("conduit_audio_requests_total", metrics);
            Assert.Contains("conduit_audio_request_duration_seconds", metrics);
            Assert.Contains("conduit_audio_cache_hit_ratio", metrics);
            Assert.Contains("conduit_audio_provider_error_rate", metrics);
            Assert.Contains("conduit_audio_active_operations", metrics);
            Assert.Contains("conduit_audio_cost_dollars", metrics);

            // Check for proper Prometheus format
            Assert.Contains("# HELP", metrics);
            Assert.Contains("# TYPE", metrics);

            // Check for labels
            Assert.Contains("operation=\"transcription\"", metrics);
            Assert.Contains("operation=\"tts\"", metrics);
            Assert.Contains("provider=\"openai\"", metrics);

            // Check for values
            Assert.Contains("{operation=\"transcription\"} 5", metrics); // Active transcriptions
            Assert.Contains("{operation=\"tts\"} 3", metrics); // Active TTS
            Assert.Contains("{operation=\"transcription\"} 0.2500", metrics); // Cache hit ratio
        }

        [Fact]
        public async Task GetMetricsAsync_CachesMetricsWithinExpiration()
        {
            // Arrange
            SetupMockMetrics();

            // Act
            // Don't start the hosted service to avoid background timer
            var metrics1 = await _exporter.GetMetricsAsync();
            var metrics2 = await _exporter.GetMetricsAsync();

            // Assert
            Assert.Equal(metrics1, metrics2);
            _mockMetricsCollector.Verify(
                m => m.GetAggregatedMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null),
                Times.Once);
        }

        [Fact]
        public async Task GetMetricsAsync_RefreshesMetricsAfterExpiration()
        {
            // Arrange
            SetupMockMetrics();
            _options.CacheExpiration = TimeSpan.FromMilliseconds(100);
            
            // Create a new exporter without starting it to avoid background timer
            var exporter = new PrometheusAudioMetricsExporter(
                _mockMetricsCollector.Object,
                _mockLogger.Object,
                Options.Create(_options));

            // Act
            var metrics1 = await exporter.GetMetricsAsync();
            await Task.Delay(150); // Wait for cache to expire
            var metrics2 = await exporter.GetMetricsAsync();

            // Assert
            _mockMetricsCollector.Verify(
                m => m.GetAggregatedMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null),
                Times.Exactly(2));
            
            // Cleanup
            exporter.Dispose();
        }

        [Fact]
        public async Task GetMetricsAsync_IncludesHistogramBuckets()
        {
            // Arrange
            SetupMockMetrics();

            // Act
            await _exporter.StartAsync(CancellationToken.None);
            var metrics = await _exporter.GetMetricsAsync();

            // Assert
            // Check for histogram buckets
            Assert.Contains("_bucket{", metrics);
            Assert.Contains("le=\"0.1\"", metrics);
            Assert.Contains("le=\"1.0\"", metrics);
            Assert.Contains("le=\"5.0\"", metrics);
            Assert.Contains("le=\"Infinity\"", metrics);
            Assert.Contains("_sum{", metrics);
            Assert.Contains("_count{", metrics);
        }

        [Fact]
        public async Task GetMetricsAsync_HandlesCostMetrics()
        {
            // Arrange
            SetupMockMetrics();

            // Act
            await _exporter.StartAsync(CancellationToken.None);
            var metrics = await _exporter.GetMetricsAsync();

            // Assert
            Assert.Contains("conduit_audio_cost_dollars{operation=\"transcription\"} 1.2500", metrics);
            Assert.Contains("conduit_audio_cost_dollars{operation=\"tts\"} 0.8000", metrics);
            Assert.Contains("conduit_audio_cost_dollars{operation=\"total\"} 3.2500", metrics);
            Assert.Contains("conduit_audio_cost_dollars{operation=\"cache_savings\"} 0.3500", metrics);
        }

        [Fact]
        public async Task StartAsync_StartsPeriodicExport()
        {
            // Arrange
            SetupMockMetrics();
            _options.ExportInterval = TimeSpan.FromMilliseconds(100);

            // Act
            await _exporter.StartAsync(CancellationToken.None);
            await Task.Delay(250);

            // Assert
            // Should have exported at least twice
            _mockMetricsCollector.Verify(
                m => m.GetAggregatedMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null),
                Times.AtLeast(2));
        }

        [Fact]
        public async Task StopAsync_StopsPeriodicExport()
        {
            // Arrange
            SetupMockMetrics();
            _options.ExportInterval = TimeSpan.FromMilliseconds(100);

            // Act
            await _exporter.StartAsync(CancellationToken.None);
            await Task.Delay(150);
            await _exporter.StopAsync(CancellationToken.None);
            
            var callCount = _mockMetricsCollector.Invocations
                .Count(i => i.Method.Name == "GetAggregatedMetricsAsync");
            
            await Task.Delay(200);
            
            var newCallCount = _mockMetricsCollector.Invocations
                .Count(i => i.Method.Name == "GetAggregatedMetricsAsync");

            // Assert
            Assert.Equal(callCount, newCallCount); // No new calls after stopping
        }

        private void SetupMockMetrics()
        {
            var aggregatedMetrics = new AggregatedAudioMetrics
            {
                Period = new DateTimeRange 
                { 
                    Start = DateTime.UtcNow.AddMinutes(-5), 
                    End = DateTime.UtcNow 
                },
                Transcription = new OperationStatistics
                {
                    TotalRequests = 100,
                    SuccessfulRequests = 95,
                    FailedRequests = 5,
                    AverageDurationMs = 2500,
                    P95DurationMs = 4000,
                    P99DurationMs = 5000,
                    CacheHitRate = 0.25,
                    TotalDataBytes = 1024000
                },
                TextToSpeech = new OperationStatistics
                {
                    TotalRequests = 50,
                    SuccessfulRequests = 48,
                    FailedRequests = 2,
                    AverageDurationMs = 1500,
                    P95DurationMs = 2000,
                    P99DurationMs = 2500,
                    CacheHitRate = 0.40,
                    TotalDataBytes = 512000
                },
                Realtime = new RealtimeStatistics
                {
                    TotalSessions = 10,
                    AverageSessionDurationSeconds = 120,
                    TotalAudioMinutes = 20,
                    AverageLatencyMs = 150,
                    DisconnectReasons = new Dictionary<string, long>
                    {
                        { "client_disconnect", 5 }
                    }
                },
                ProviderStats = new Dictionary<string, ProviderStatistics>
                {
                    {
                        "openai", new ProviderStatistics
                        {
                            Provider = "openai",
                            RequestCount = 150,
                            SuccessRate = 0.96,
                            AverageLatencyMs = 2000,
                            UptimePercentage = 99.5,
                            ErrorBreakdown = new Dictionary<string, long>()
                        }
                    }
                },
                Costs = new CostAnalysis
                {
                    TranscriptionCost = 1.25m,
                    TextToSpeechCost = 0.80m,
                    RealtimeCost = 1.20m,
                    TotalCost = 3.25m,
                    CachingSavings = 0.35m
                }
            };

            var snapshot = new AudioMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                ActiveTranscriptions = 5,
                ActiveTtsOperations = 3,
                ActiveRealtimeSessions = 2,
                RequestsPerSecond = 2.5,
                CurrentErrorRate = 0.04,
                ProviderHealth = new Dictionary<string, bool> { { "openai", true } },
                Resources = new SystemResources
                {
                    CpuUsagePercent = 45.5,
                    MemoryUsageMb = 2048,
                    ActiveConnections = 25,
                    CacheSizeMb = 512
                }
            };

            _mockMetricsCollector
                .Setup(m => m.GetAggregatedMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                .ReturnsAsync(aggregatedMetrics);

            _mockMetricsCollector
                .Setup(m => m.GetCurrentSnapshotAsync())
                .ReturnsAsync(snapshot);
        }

        public void Dispose()
        {
            _exporter?.Dispose();
        }
    }
}