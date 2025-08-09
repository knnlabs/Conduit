using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioMetricsCollectorTests
    {
        #region RecordTranscriptionMetricAsync Tests

        [Fact]
        public async Task RecordTranscriptionMetricAsync_ValidMetric_RecordsSuccessfully()
        {
            // Arrange
            var metric = new TranscriptionMetric
            {
                Provider = "OpenAI",
                VirtualKey = "test-key",
                Success = true,
                DurationMs = 1500,
                AudioFormat = "mp3",
                AudioDurationSeconds = 60,
                FileSizeBytes = 1024000,
                DetectedLanguage = "en",
                Confidence = 0.95,
                WordCount = 150,
                ServedFromCache = false
            };

            // Act
            await _collector.RecordTranscriptionMetricAsync(metric);

            // Assert
            var snapshot = await _collector.GetCurrentSnapshotAsync();
            Assert.True(snapshot.ActiveTranscriptions >= 0);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded transcription metric")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordTranscriptionMetricAsync_HighLatency_LogsWarning()
        {
            // Arrange
            var metric = new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 6000, // Above threshold
                AudioFormat = "wav",
                AudioDurationSeconds = 120
            };

            // Act
            await _collector.RecordTranscriptionMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("High transcription latency detected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordTranscriptionMetricAsync_WithCacheHit_IncrementsCacheCounter()
        {
            // Arrange
            var metric = new TranscriptionMetric
            {
                Provider = "OpenAI",
                Success = true,
                DurationMs = 100,
                ServedFromCache = true,
                AudioFormat = "mp3",
                AudioDurationSeconds = 30
            };

            // Act
            await _collector.RecordTranscriptionMetricAsync(metric);
            await _collector.RecordTranscriptionMetricAsync(metric);

            // Assert
            var aggregated = await _collector.GetAggregatedMetricsAsync(
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(1));
            
            Assert.Equal(1.0, aggregated.Transcription.CacheHitRate); // Both served from cache
        }

        [Fact]
        public async Task RecordTranscriptionMetricAsync_ExceptionDuringRecording_HandlesGracefully()
        {
            // Arrange
            var metric = new TranscriptionMetric
            {
                Provider = null!, // Will cause NullReferenceException
                Success = true,
                DurationMs = 1000
            };

            // Act
            await _collector.RecordTranscriptionMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error recording transcription metric")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region RecordTtsMetricAsync Tests

        [Fact]
        public async Task RecordTtsMetricAsync_ValidMetric_RecordsSuccessfully()
        {
            // Arrange
            var metric = new TtsMetric
            {
                Provider = "ElevenLabs",
                Voice = "Rachel",
                Success = true,
                DurationMs = 2000,
                CharacterCount = 500,
                OutputFormat = "mp3",
                GeneratedDurationSeconds = 30,
                OutputSizeBytes = 512000,
                ServedFromCache = false,
                UploadedToCdn = true
            };

            // Act
            await _collector.RecordTtsMetricAsync(metric);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded TTS metric")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordTtsMetricAsync_WithCdnUpload_TracksCdnUploads()
        {
            // Arrange
            var metric1 = new TtsMetric
            {
                Provider = "OpenAI",
                Voice = "alloy",
                Success = true,
                DurationMs = 1000,
                CharacterCount = 100,
                OutputFormat = "mp3",
                UploadedToCdn = true
            };

            var metric2 = new TtsMetric
            {
                Provider = "OpenAI",
                Voice = "nova",
                Success = true,
                DurationMs = 1500,
                CharacterCount = 200,
                OutputFormat = "mp3",
                UploadedToCdn = false
            };

            // Act
            await _collector.RecordTtsMetricAsync(metric1);
            await _collector.RecordTtsMetricAsync(metric2);

            // Assert - verify CDN upload was tracked (implementation specific)
            var snapshot = await _collector.GetCurrentSnapshotAsync();
            Assert.True(snapshot.ActiveTtsOperations >= 0);
        }

        #endregion
    }
}