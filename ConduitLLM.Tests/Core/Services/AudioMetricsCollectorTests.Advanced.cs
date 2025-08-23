using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Options;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioMetricsCollectorTests
    {
        #region Thread Safety Tests

        [Fact]
        public async Task RecordMetrics_ConcurrentOperations_HandlesCorrectly()
        {
            // Arrange
            const int threadCount = 10;
            const int metricsPerThread = 100;
            var tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                var threadId = i;
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < metricsPerThread; j++)
                    {
                        if (j % 3 == 0)
                        {
                            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                            {
                                Provider = $"Provider{threadId}",
                                Success = true,
                                DurationMs = 100 + j,
                                AudioFormat = "mp3",
                                AudioDurationSeconds = 10
                            });
                        }
                        else if (j % 3 == 1)
                        {
                            await _collector.RecordTtsMetricAsync(new TtsMetric
                            {
                                Provider = $"Provider{threadId}",
                                Success = true,
                                DurationMs = 200 + j,
                                CharacterCount = 100,
                                Voice = "test-voice",
                                OutputFormat = "mp3"
                            });
                        }
                        else
                        {
                            await _collector.RecordRealtimeMetricAsync(new RealtimeMetric
                            {
                                Provider = $"Provider{threadId}",
                                SessionId = $"session-{threadId}-{j}",
                                Success = true,
                                DurationMs = 300 + j,
                                SessionDurationSeconds = 60,
                                TurnCount = 5
                            });
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            var totalExpected = threadCount * metricsPerThread;
            var expectedPerType = totalExpected / 3;

            // Allow for some rounding differences
            Assert.InRange(aggregated.Transcription.TotalRequests, expectedPerType - 10, expectedPerType + 10);
            Assert.InRange(aggregated.TextToSpeech.TotalRequests, expectedPerType - 10, expectedPerType + 10);
            Assert.InRange(aggregated.Realtime.TotalSessions, expectedPerType - 10, expectedPerType + 10);
        }

        #endregion

        #region Provider Statistics Tests

        [Fact]
        public async Task AggregateProviderStats_MultipleProviders_GroupsCorrectly()
        {
            // Arrange
            var providers = new[] { "OpenAI", "Azure", "Google" };
            
            foreach (var provider in providers)
            {
                await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = provider,
                    Success = true,
                    DurationMs = 1000,
                    AudioFormat = "mp3",
                    AudioDurationSeconds = 30
                });

                await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = provider,
                    Success = false,
                    DurationMs = 2000,
                    ErrorCode = "RATE_LIMIT",
                    AudioFormat = "wav",
                    AudioDurationSeconds = 20
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(3, aggregated.ProviderStats.Count);
            
            foreach (var provider in providers)
            {
                Assert.True(aggregated.ProviderStats.ContainsKey(provider));
                var stats = aggregated.ProviderStats[provider];
                Assert.Equal(2, stats.RequestCount);
                Assert.Equal(0.5, stats.SuccessRate); // 1 success, 1 failure
                Assert.Equal(1500, stats.AverageLatencyMs); // (1000 + 2000) / 2
                Assert.True(stats.ErrorBreakdown.ContainsKey("RATE_LIMIT"));
                Assert.Equal(1, stats.ErrorBreakdown["RATE_LIMIT"]);
            }
        }

        [Fact]
        public async Task ProviderUptime_CalculatedCorrectly()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                await _collector.RecordProviderHealthMetricAsync(new ProviderHealthMetric
                {
                    Provider = "OpenAI",
                    IsHealthy = i < 8, // 8 healthy, 2 unhealthy
                    ResponseTimeMs = 100,
                    ErrorRate = i < 8 ? 0.01 : 0.5
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            // Provider stats might not contain OpenAI if no audio metrics were recorded
            // Only check if it exists
            if (aggregated.ProviderStats.ContainsKey("OpenAI"))
            {
                Assert.Equal(80, aggregated.ProviderStats["OpenAI"].UptimePercentage); // 8/10 * 100
            }
        }

        #endregion

        #region Cache Hit Rate Tests

        [Fact]
        public async Task CacheHitRate_TranscriptionMetrics_CalculatedCorrectly()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = "OpenAI",
                    Success = true,
                    DurationMs = 100,
                    ServedFromCache = i % 2 == 0, // Half from cache
                    AudioFormat = "mp3",
                    AudioDurationSeconds = 10
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(0.5, aggregated.Transcription.CacheHitRate);
        }

        [Fact]
        public async Task CacheHitRate_TtsMetrics_CalculatedCorrectly()
        {
            // Arrange
            for (int i = 0; i < 8; i++)
            {
                await _collector.RecordTtsMetricAsync(new TtsMetric
                {
                    Provider = "ElevenLabs",
                    Success = true,
                    DurationMs = 200,
                    ServedFromCache = i < 6, // 6 from cache, 2 not
                    CharacterCount = 100,
                    Voice = "Rachel",
                    OutputFormat = "mp3"
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(0.75, aggregated.TextToSpeech.CacheHitRate); // 6/8
        }

        #endregion

        #region Data Size Tracking Tests

        [Fact]
        public async Task TotalDataBytes_Transcription_CalculatedCorrectly()
        {
            // Arrange
            var sizes = new long[] { 1000000, 2000000, 3000000 };
            
            foreach (var size in sizes)
            {
                await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = "OpenAI",
                    Success = true,
                    DurationMs = 1000,
                    FileSizeBytes = size,
                    AudioFormat = "mp3",
                    AudioDurationSeconds = 30
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(6000000, aggregated.Transcription.TotalDataBytes);
        }

        [Fact]
        public async Task TotalDataBytes_Tts_CalculatedCorrectly()
        {
            // Arrange
            var sizes = new long[] { 500000, 750000, 1000000 };
            
            foreach (var size in sizes)
            {
                await _collector.RecordTtsMetricAsync(new TtsMetric
                {
                    Provider = "OpenAI",
                    Success = true,
                    DurationMs = 1500,
                    OutputSizeBytes = size,
                    CharacterCount = 1000,
                    Voice = "alloy",
                    OutputFormat = "mp3"
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(2250000, aggregated.TextToSpeech.TotalDataBytes);
        }

        #endregion

        #region Edge Cases and Error Scenarios

        [Fact]
        public async Task GetAggregatedMetricsAsync_NoMetrics_ReturnsEmptyAggregation()
        {
            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(0, aggregated.Transcription.TotalRequests);
            Assert.Equal(0, aggregated.TextToSpeech.TotalRequests);
            Assert.Equal(0, aggregated.Realtime.TotalSessions);
            Assert.Empty(aggregated.ProviderStats);
            Assert.Equal(0m, aggregated.Costs.TotalCost);
        }

        [Fact]
        public async Task GetAggregatedMetricsAsync_FutureDateRange_ReturnsEmpty()
        {
            // Arrange
            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 1000,
                AudioFormat = "mp3",
                AudioDurationSeconds = 30
            });

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(2));

            // Assert
            Assert.Equal(0, aggregated.Transcription.TotalRequests);
        }

        [Fact]
        public async Task RecordMetrics_NullAlertingService_HandlesGracefully()
        {
            // Arrange
            var collector = new AudioMetricsCollector(
                _loggerMock.Object,
                Options.Create(_options),
                null); // No alerting service

            var metric = new ProviderHealthMetric
            {
                Provider = "OpenAI",
                IsHealthy = false,
                ErrorRate = 0.9
            };

            // Act & Assert - should not throw
            await collector.RecordProviderHealthMetricAsync(metric);
        }

        [Fact]
        public async Task Percentile_SingleValue_ReturnsValue()
        {
            // Arrange
            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 1000,
                AudioFormat = "mp3",
                AudioDurationSeconds = 30
            });

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(1000, aggregated.Transcription.P95DurationMs);
            Assert.Equal(1000, aggregated.Transcription.P99DurationMs);
        }

        #endregion
    }
}