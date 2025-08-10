using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region GetUsageByProviderAsync Tests

        [Fact]
        public async Task GetUsageByProviderAsync_WithValidProvider_ShouldReturnProviderUsage()
        {
            // Arrange
            var providerId = 1;
            var logs = new List<AudioUsageLog>
            {
                CreateAudioUsageLog("transcription", "whisper-1", 200),
                CreateAudioUsageLog("tts", "tts-1", 200),
                CreateAudioUsageLog("realtime", "gpt-4o-realtime", 200),
                CreateAudioUsageLog("transcription", "whisper-1", 500) // Failed request
            };

            _mockRepository.Setup(x => x.GetByProviderAsync(providerId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(logs);

            // Act
            var result = await _service.GetUsageByProviderAsync(providerId);

            // Assert
            result.Should().NotBeNull();
            result.ProviderId.Should().Be(providerId);
            result.TotalOperations.Should().Be(4);
            result.TranscriptionCount.Should().Be(2);
            result.TextToSpeechCount.Should().Be(1);
            result.RealtimeSessionCount.Should().Be(1);
            result.SuccessRate.Should().Be(75); // 3 successful out of 4
            result.MostUsedModel.Should().Be("whisper-1");
        }

        [Fact]
        public async Task GetUsageByProviderAsync_WithNoLogs_ShouldReturnZeroMetrics()
        {
            // Arrange
            var providerId = 2;
            _mockRepository.Setup(x => x.GetByProviderAsync(providerId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new List<AudioUsageLog>());

            // Act
            var result = await _service.GetUsageByProviderAsync(providerId);

            // Assert
            result.TotalOperations.Should().Be(0);
            result.SuccessRate.Should().Be(0);
            result.MostUsedModel.Should().BeNull();
        }

        #endregion
    }
}