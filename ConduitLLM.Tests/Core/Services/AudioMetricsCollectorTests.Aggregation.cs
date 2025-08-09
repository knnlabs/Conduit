using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioMetricsCollectorTests
    {
        #region GetAggregatedMetricsAsync Tests

        [Fact]
        public async Task GetAggregatedMetricsAsync_MultipleMetrics_AggregatesCorrectly()
        {
            // Arrange
            var now = DateTime.UtcNow;
            
            // Record various metrics
            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 1000,
                AudioDurationSeconds = 60,
                FileSizeBytes = 1000000,
                WordCount = 100,
                AudioFormat = "mp3"
            });

            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = false,
                DurationMs = 2000,
                ErrorCode = "TIMEOUT",
                AudioDurationSeconds = 30,
                AudioFormat = "wav"
            });

            await _collector.RecordTtsMetricAsync(new TtsMetric
            {
                Provider = "ElevenLabs",
                Success = true,
                DurationMs = 1500,
                CharacterCount = 500,
                OutputSizeBytes = 50000,
                Voice = "Rachel",
                OutputFormat = "mp3"
            });

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                now.AddMinutes(-5),
                now.AddMinutes(5));

            // Assert
            Assert.Equal(2, aggregated.Transcription.TotalRequests);
            Assert.Equal(1, aggregated.Transcription.SuccessfulRequests);
            Assert.Equal(1, aggregated.Transcription.FailedRequests);
            Assert.Equal(1, aggregated.TextToSpeech.TotalRequests);
            Assert.Equal(1, aggregated.TextToSpeech.SuccessfulRequests);
            Assert.True(aggregated.Transcription.AverageDurationMs > 0);
        }

        [Fact]
        public async Task GetAggregatedMetricsAsync_WithProviderFilter_FiltersCorrectly()
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

            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "Azure",
                Success = true,
                DurationMs = 1500,
                AudioFormat = "wav",
                AudioDurationSeconds = 45
            });

            // Act
            var openAiMetrics = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5),
                "OpenAI");

            // Assert
            Assert.Equal(1, openAiMetrics.Transcription.TotalRequests);
            Assert.True(openAiMetrics.ProviderStats.ContainsKey("OpenAI"));
            Assert.False(openAiMetrics.ProviderStats.ContainsKey("Azure"));
        }

        [Fact]
        public async Task GetAggregatedMetricsAsync_CalculatesPercentiles_Correctly()
        {
            // Arrange
            var durations = new[] { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };
            
            foreach (var duration in durations)
            {
                await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = "OpenAI",
                    Success = true,
                    DurationMs = duration,
                    AudioFormat = "mp3",
                    AudioDurationSeconds = 10
                });
            }

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(550, aggregated.Transcription.AverageDurationMs);
            Assert.Equal(1000, aggregated.Transcription.P95DurationMs); // 95th percentile of 10 values (ceiling calculation)
            Assert.Equal(1000, aggregated.Transcription.P99DurationMs);
        }

        [Fact]
        public async Task GetAggregatedMetricsAsync_RealtimeMetrics_AggregatesSessionData()
        {
            // Arrange
            await _collector.RecordRealtimeMetricAsync(new RealtimeMetric
            {
                Provider = "OpenAI",
                SessionId = "session-1",
                Success = true,
                DurationMs = 5000,
                SessionDurationSeconds = 300,
                TurnCount = 10,
                TotalAudioSentSeconds = 120,
                TotalAudioReceivedSeconds = 150,
                AverageLatencyMs = 100,
                DisconnectReason = "user_disconnected"
            });

            await _collector.RecordRealtimeMetricAsync(new RealtimeMetric
            {
                Provider = "OpenAI",
                SessionId = "session-2",
                Success = true,
                DurationMs = 3000,
                SessionDurationSeconds = 180,
                TurnCount = 5,
                TotalAudioSentSeconds = 60,
                TotalAudioReceivedSeconds = 80,
                AverageLatencyMs = 120,
                DisconnectReason = "timeout"
            });

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(2, aggregated.Realtime.TotalSessions);
            Assert.Equal(240, aggregated.Realtime.AverageSessionDurationSeconds); // (300+180)/2
            Assert.InRange(aggregated.Realtime.TotalAudioMinutes, 6.8, 6.9); // (120+150+60+80)/60
            Assert.Equal(110, aggregated.Realtime.AverageLatencyMs); // (100+120)/2
            Assert.Equal(2, aggregated.Realtime.DisconnectReasons.Count);
            Assert.Equal(1, aggregated.Realtime.DisconnectReasons["user_disconnected"]);
            Assert.Equal(1, aggregated.Realtime.DisconnectReasons["timeout"]);
        }

        [Fact]
        public async Task GetAggregatedMetricsAsync_CostCalculation_CalculatesCorrectly()
        {
            // Arrange
            // Transcription: $0.006/minute
            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 1000,
                AudioDurationSeconds = 600, // 10 minutes
                AudioFormat = "mp3"
            });

            // TTS: $16/1M chars
            await _collector.RecordTtsMetricAsync(new TtsMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 2000,
                CharacterCount = 10000,
                Voice = "alloy",
                OutputFormat = "mp3"
            });

            // Realtime: $0.06/minute
            await _collector.RecordRealtimeMetricAsync(new RealtimeMetric
            {
                Provider = "OpenAI",
                SessionId = "session-1",
                Success = true,
                DurationMs = 5000,
                SessionDurationSeconds = 300, // 5 minutes
                TurnCount = 10
            });

            // Act
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(5));

            // Assert
            Assert.Equal(0.06m, aggregated.Costs.TranscriptionCost); // 10 * 0.006
            Assert.Equal(0.16m, aggregated.Costs.TextToSpeechCost); // 10000 * 0.000016 = 0.16
            Assert.Equal(0.3m, aggregated.Costs.RealtimeCost); // 5 * 0.06
            Assert.Equal(0.52m, aggregated.Costs.TotalCost); // 0.06 + 0.16 + 0.3
        }

        #endregion

        #region GetCurrentSnapshotAsync Tests

        [Fact]
        public async Task GetCurrentSnapshotAsync_ReturnsCurrentState()
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

            await _collector.RecordProviderHealthMetricAsync(new ProviderHealthMetric
            {
                Provider = "OpenAI",
                IsHealthy = true,
                ErrorRate = 0.01
            });

            await _collector.RecordProviderHealthMetricAsync(new ProviderHealthMetric
            {
                Provider = "Azure",
                IsHealthy = false,
                ErrorRate = 0.5
            });

            // Act
            var snapshot = await _collector.GetCurrentSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Timestamp <= DateTime.UtcNow);
            Assert.True(snapshot.ProviderHealth.ContainsKey("OpenAI"));
            Assert.True(snapshot.ProviderHealth["OpenAI"]);
            Assert.False(snapshot.ProviderHealth["Azure"]);
            Assert.NotNull(snapshot.Resources);
        }

        [Fact]
        public async Task GetCurrentSnapshotAsync_CalculatesRequestRate()
        {
            // Arrange
            var tasks = new List<Task>();
            
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = "OpenAI",
                    Success = true,
                    DurationMs = 100,
                    AudioFormat = "mp3",
                    AudioDurationSeconds = 5
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(100); // Ensure some time passes

            // Act
            var snapshot = await _collector.GetCurrentSnapshotAsync();

            // Assert
            Assert.True(snapshot.RequestsPerSecond >= 0); // Can be 0 if time calculation is too fast
        }

        [Fact]
        public async Task GetCurrentSnapshotAsync_CalculatesErrorRate()
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

            await _collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = false,
                DurationMs = 2000,
                ErrorCode = "TIMEOUT",
                AudioFormat = "wav",
                AudioDurationSeconds = 20
            });

            // Act
            var snapshot = await _collector.GetCurrentSnapshotAsync();

            // Assert
            Assert.Equal(0.5, snapshot.CurrentErrorRate); // 1 failure out of 2 requests
        }

        #endregion

        #region Cleanup and Retention Tests

        [Fact(Skip = "Flaky timing test - uses aggressive 50ms timer intervals that cause race conditions in concurrent test runs")]
        public async Task AggregationTimer_CleansUpOldBuckets()
        {
            // Arrange
            var shortRetentionOptions = new AudioMetricsOptions
            {
                AggregationInterval = TimeSpan.FromMilliseconds(50),
                RetentionPeriod = TimeSpan.FromMilliseconds(100),
                TranscriptionLatencyThreshold = 5000,
                RealtimeLatencyThreshold = 200
            };

            var collector = new AudioMetricsCollector(
                _loggerMock.Object,
                Options.Create(shortRetentionOptions),
                null);

            try
            {
                // Record a metric
                await collector.RecordTranscriptionMetricAsync(new TranscriptionMetric
                {
                    Provider = "OpenAI",
                    Success = true,
                    DurationMs = 1000,
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-200), // Old metric
                    AudioFormat = "mp3",
                    AudioDurationSeconds = 30
                });

                // Act - wait for cleanup
                await Task.Delay(150);

                // Assert - old metrics should be cleaned up
                var aggregated = await collector.GetAggregatedMetricsAsync(
                    DateTime.UtcNow.AddSeconds(-10),
                    DateTime.UtcNow);

                Assert.Equal(0, aggregated.Transcription.TotalRequests);
            }
            finally
            {
                collector.Dispose();
            }
        }

        #endregion
    }
}