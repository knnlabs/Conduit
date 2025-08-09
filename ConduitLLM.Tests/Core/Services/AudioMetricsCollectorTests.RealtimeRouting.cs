using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioMetricsCollectorTests
    {
        #region RecordRealtimeMetricAsync Tests

        [Fact]
        public async Task RecordRealtimeMetricAsync_ValidMetric_RecordsSuccessfully()
        {
            // Arrange
            var metric = new RealtimeMetric
            {
                Provider = "OpenAI",
                SessionId = "session-123",
                Success = true,
                DurationMs = 5000,
                SessionDurationSeconds = 300,
                TurnCount = 10,
                TotalAudioSentSeconds = 120,
                TotalAudioReceivedSeconds = 150,
                AverageLatencyMs = 150,
                DisconnectReason = "user_disconnected"
            };

            // Act
            await _collector.RecordRealtimeMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded realtime metric")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordRealtimeMetricAsync_HighLatency_LogsWarning()
        {
            // Arrange
            var metric = new RealtimeMetric
            {
                Provider = "OpenAI",
                SessionId = "session-456",
                Success = true,
                DurationMs = 5000,
                AverageLatencyMs = 250, // Above threshold of 200ms
                SessionDurationSeconds = 60,
                TurnCount = 5
            };

            // Act
            await _collector.RecordRealtimeMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("High realtime latency detected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region RecordRoutingMetricAsync Tests

        [Fact]
        public async Task RecordRoutingMetricAsync_ValidMetric_RecordsSuccessfully()
        {
            // Arrange
            var metric = new RoutingMetric
            {
                Provider = "OpenAI",
                Operation = AudioOperation.Transcription,
                RoutingStrategy = "least-cost",
                SelectedProvider = "OpenAI",
                CandidateProviders = new List<string> { "OpenAI", "Azure", "Google" },
                DecisionTimeMs = 50,
                RoutingReason = "Lowest cost provider available",
                Success = true,
                DurationMs = 100
            };

            // Act
            await _collector.RecordRoutingMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded routing metric")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region RecordProviderHealthMetricAsync Tests

        [Fact]
        public async Task RecordProviderHealthMetricAsync_HealthyProvider_RecordsSuccessfully()
        {
            // Arrange
            var metric = new ProviderHealthMetric
            {
                Provider = "OpenAI",
                IsHealthy = true,
                ResponseTimeMs = 100,
                ErrorRate = 0.01,
                SuccessRate = 0.99,
                ActiveConnections = 10,
                HealthDetails = new Dictionary<string, object>
                {
                    ["api_version"] = "v1",
                    ["region"] = "us-east-1"
                }
            };

            // Act
            await _collector.RecordProviderHealthMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded provider health")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordProviderHealthMetricAsync_UnhealthyProvider_TriggersAlerting()
        {
            // Arrange
            var metric = new ProviderHealthMetric
            {
                Provider = "Azure",
                IsHealthy = false,
                ResponseTimeMs = 5000,
                ErrorRate = 0.5,
                SuccessRate = 0.5,
                ActiveConnections = 0
            };

            _alertingServiceMock.Setup(x => x.EvaluateMetricsAsync(
                It.IsAny<AudioMetricsSnapshot>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _collector.RecordProviderHealthMetricAsync(metric);

            // Assert - wait longer for async alerting to complete
            // The alerting is done in a fire-and-forget Task.Run which needs time to execute
            await Task.Delay(500); // Increased from 100ms to 500ms
            _alertingServiceMock.Verify(x => x.EvaluateMetricsAsync(
                It.IsAny<AudioMetricsSnapshot>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion
    }
}