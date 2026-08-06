using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using AwesomeAssertions;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for audio (speech-to-text per-minute, text-to-speech per-thousand-characters) billing.
    /// </summary>
    public partial class CostCalculationServiceBasicTests
    {
        [Fact]
        public async Task CalculateCostAsync_AudioTranscription_BillsPerMinute()
        {
            // Arrange — 2 minutes of audio at $0.006/min
            var usage = new Usage { AudioDurationSeconds = 120 };
            var modelCost = new ModelCost
            {
                CostName = "whisper",
                AudioCostPerMinute = 0.006m,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync("openai/whisper-1", usage);

            // Assert
            result.Should().Be(0.012m);
        }

        [Fact]
        public async Task CalculateCostAsync_TextToSpeech_BillsPerThousandCharacters()
        {
            // Arrange — 500 characters at $0.015 per 1,000
            var usage = new Usage { TtsCharacters = 500 };
            var modelCost = new ModelCost
            {
                CostName = "tts",
                AudioCostPerThousandCharacters = 0.015m,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync("openai/tts-1", usage);

            // Assert
            result.Should().Be(0.0075m);
        }

        [Fact]
        public async Task CalculateCostAsync_AudioWithoutConfiguredCost_BillsZero()
        {
            // Arrange — audio usage but no audio cost configured
            var usage = new Usage { AudioDurationSeconds = 120, TtsCharacters = 500 };
            var modelCost = new ModelCost
            {
                CostName = "chat-only",
                InputCostPerMillionTokens = 10m,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync("some/model", usage);

            // Assert — no token usage, no audio cost configured
            result.Should().Be(0m);
        }
    }
}
