using System;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class AudioQualityTrackerTests
    {
        private readonly Mock<ILogger<AudioQualityTracker>> _mockLogger;
        private readonly Mock<IAudioMetricsCollector> _mockMetricsCollector;
        private readonly AudioQualityTracker _tracker;

        public AudioQualityTrackerTests()
        {
            _mockLogger = new Mock<ILogger<AudioQualityTracker>>();
            _mockMetricsCollector = new Mock<IAudioMetricsCollector>();
            _tracker = new AudioQualityTracker(_mockLogger.Object, _mockMetricsCollector.Object);
        }

        [Fact]
        public async Task TrackTranscriptionQualityAsync_HighConfidence_NoWarnings()
        {
            // Arrange
            var metric = new AudioQualityMetric
            {
                Provider = "openai",
                Model = "whisper-1",
                VirtualKey = "test-key",
                Confidence = 0.95,
                WordErrorRate = 0.02,
                AccuracyScore = 0.98,
                Language = "en",
                AudioDurationSeconds = 60,
                ProcessingDurationMs = 1500
            };

            _mockMetricsCollector
                .Setup(x => x.RecordTranscriptionMetricAsync(It.IsAny<TranscriptionMetric>()))
                .Returns(Task.CompletedTask);

            // Act
            await _tracker.TrackTranscriptionQualityAsync(metric);

            // Assert
            _mockMetricsCollector.Verify(
                x => x.RecordTranscriptionMetricAsync(It.Is<TranscriptionMetric>(
                    m => m.Confidence == 0.95 && 
                         m.Provider == "openai" &&
                         m.Tags["quality.wer"] == "0.020")),
                Times.Once);

            // No warnings should be logged for high confidence
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task TrackTranscriptionQualityAsync_LowConfidence_LogsWarning()
        {
            // Arrange
            var metric = new AudioQualityMetric
            {
                Provider = "azure",
                Confidence = 0.65, // Below 0.7 threshold
                WordErrorRate = 0.05,
                Language = "fr",
                AudioDurationSeconds = 30,
                ProcessingDurationMs = 1000
            };

            // Act
            await _tracker.TrackTranscriptionQualityAsync(metric);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Low confidence transcription")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task TrackTranscriptionQualityAsync_HighWordErrorRate_LogsWarning()
        {
            // Arrange
            var metric = new AudioQualityMetric
            {
                Provider = "google",
                Confidence = 0.85,
                WordErrorRate = 0.20, // Above 0.15 threshold
                Language = "es",
                AudioDurationSeconds = 45,
                ProcessingDurationMs = 1200
            };

            // Act
            await _tracker.TrackTranscriptionQualityAsync(metric);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("High word error rate")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetQualityReportAsync_ReturnsAggregatedData()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;

            // Track some metrics
            await _tracker.TrackTranscriptionQualityAsync(new AudioQualityMetric
            {
                Provider = "openai",
                Confidence = 0.90,
                AccuracyScore = 0.92,
                Language = "en",
                AudioDurationSeconds = 30,
                ProcessingDurationMs = 1000
            });

            await _tracker.TrackTranscriptionQualityAsync(new AudioQualityMetric
            {
                Provider = "openai",
                Confidence = 0.85,
                AccuracyScore = 0.88,
                Language = "en",
                AudioDurationSeconds = 45,
                ProcessingDurationMs = 1500
            });

            // Act
            var report = await _tracker.GetQualityReportAsync(startTime, endTime, "openai");

            // Assert
            Assert.NotNull(report);
            Assert.Contains("openai", report.ProviderQuality.Keys);
            
            var providerStats = report.ProviderQuality["openai"];
            Assert.Equal(0.875, providerStats.AverageConfidence, 3);
            Assert.Equal(0.90, providerStats.AverageAccuracy, 3);
            Assert.Equal(2, providerStats.SampleCount);
        }

        [Fact]
        public async Task IsQualityAcceptableAsync_AboveThreshold_ReturnsTrue()
        {
            // Arrange & Act
            var result = await _tracker.IsQualityAcceptableAsync("openai", 0.85, 0.08);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsQualityAcceptableAsync_BelowConfidenceThreshold_ReturnsFalse()
        {
            // Arrange & Act
            var result = await _tracker.IsQualityAcceptableAsync("azure", 0.75, 0.05);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsQualityAcceptableAsync_HighWordErrorRate_ReturnsFalse()
        {
            // Arrange & Act
            var result = await _tracker.IsQualityAcceptableAsync("google", 0.90, 0.15);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetQualityThresholdsAsync_ReturnsExpectedThresholds()
        {
            // Arrange & Act
            var thresholds = await _tracker.GetQualityThresholdsAsync("openai");

            // Assert
            Assert.Equal(0.8, thresholds.MinimumConfidence);
            Assert.Equal(0.1, thresholds.MaximumWordErrorRate);
            Assert.Equal(0.9, thresholds.MinimumAccuracy);
            Assert.Equal(0.95, thresholds.OptimalConfidence);
            Assert.Equal(0.05, thresholds.OptimalWordErrorRate);
            Assert.Equal(0.97, thresholds.OptimalAccuracy);
        }

        [Fact]
        public async Task GetQualityReportAsync_GeneratesRecommendations()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;

            // Track low quality metrics
            for (int i = 0; i < 10; i++)
            {
                await _tracker.TrackTranscriptionQualityAsync(new AudioQualityMetric
                {
                    Provider = "low-quality-provider",
                    Confidence = 0.70 + (i * 0.01), // Average ~0.745
                    Language = "difficult-lang",
                    WordErrorRate = 0.18 + (i * 0.01), // Average ~0.225
                    AudioDurationSeconds = 30,
                    ProcessingDurationMs = 1000
                });
            }

            // Act
            var report = await _tracker.GetQualityReportAsync(startTime, endTime);

            // Assert
            Assert.NotEmpty(report.Recommendations);
            
            // Should recommend provider switch for low confidence
            Assert.Contains(report.Recommendations, 
                r => r.Type == RecommendationType.ProviderSwitch && 
                     r.Provider == "low-quality-provider");

            // Should recommend model upgrade for high WER language
            Assert.Contains(report.Recommendations,
                r => r.Type == RecommendationType.ModelUpgrade &&
                     r.Language == "difficult-lang");
        }

        private void Dispose()
        {
            _tracker?.Dispose();
        }
    }
}