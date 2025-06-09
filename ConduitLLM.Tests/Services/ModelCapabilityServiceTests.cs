using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Tests for the ModelCapabilityService.
    /// </summary>
    public class ModelCapabilityServiceTests
    {
        private readonly Mock<ILogger<ModelCapabilityService>> _loggerMock;
        private readonly ModelCapabilityService _service;

        public ModelCapabilityServiceTests()
        {
            _loggerMock = new Mock<ILogger<ModelCapabilityService>>();
            _service = new ModelCapabilityService(_loggerMock.Object);
        }

        [Theory]
        [InlineData("gpt-4-vision-preview", true)]
        [InlineData("gpt-4o", true)]
        [InlineData("claude-3-opus", true)]
        [InlineData("gemini-pro-vision", true)]
        [InlineData("gpt-3.5-turbo", false)]
        [InlineData("whisper-1", false)]
        public async Task SupportsVisionAsync_ReturnsExpectedResult(string model, bool expected)
        {
            // Act
            var result = await _service.SupportsVisionAsync(model);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("whisper-1", true)]
        [InlineData("whisper-large-v3", true)]
        [InlineData("gpt-4", false)]
        [InlineData("tts-1", false)]
        public async Task SupportsAudioTranscriptionAsync_ReturnsExpectedResult(string model, bool expected)
        {
            // Act
            var result = await _service.SupportsAudioTranscriptionAsync(model);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("tts-1", true)]
        [InlineData("tts-1-hd", true)]
        [InlineData("eleven_multilingual_v2", true)]
        [InlineData("eleven_turbo_v2", true)]
        [InlineData("gpt-4", false)]
        [InlineData("whisper-1", false)]
        public async Task SupportsTextToSpeechAsync_ReturnsExpectedResult(string model, bool expected)
        {
            // Act
            var result = await _service.SupportsTextToSpeechAsync(model);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("gpt-4o-realtime-preview", true)]
        [InlineData("ultravox-v0_2", true)]
        [InlineData("gpt-4", false)]
        [InlineData("tts-1", false)]
        public async Task SupportsRealtimeAudioAsync_ReturnsExpectedResult(string model, bool expected)
        {
            // Act
            var result = await _service.SupportsRealtimeAudioAsync(model);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("gpt-4", "cl100k_base")]
        [InlineData("gpt-4-turbo", "cl100k_base")]
        [InlineData("gpt-4o", "o200k_base")]
        [InlineData("gpt-3.5-turbo", "cl100k_base")]
        [InlineData("claude-3-opus", "claude")]
        [InlineData("gemini-1.5-pro", "gemini")]
        [InlineData("unknown-model", "cl100k_base")] // Default
        public async Task GetTokenizerTypeAsync_ReturnsExpectedTokenizer(string model, string expectedTokenizer)
        {
            // Act
            var result = await _service.GetTokenizerTypeAsync(model);

            // Assert
            Assert.Equal(expectedTokenizer, result);
        }

        [Fact]
        public async Task GetSupportedVoicesAsync_ForOpenAI_ReturnsCorrectVoices()
        {
            // Arrange
            var expectedVoices = new List<string> { "alloy", "echo", "fable", "nova", "onyx", "shimmer" };

            // Act
            var result = await _service.GetSupportedVoicesAsync("tts-1");

            // Assert
            Assert.Equal(expectedVoices.Count, result.Count);
            foreach (var voice in expectedVoices)
            {
                Assert.Contains(voice, result);
            }
        }

        [Fact]
        public async Task GetSupportedVoicesAsync_ForElevenLabs_ReturnsCorrectVoices()
        {
            // Act
            var result = await _service.GetSupportedVoicesAsync("eleven_multilingual_v2");

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains("rachel", result);
            Assert.Contains("drew", result);
        }

        [Fact]
        public async Task GetSupportedLanguagesAsync_ForWhisper_ReturnsLanguages()
        {
            // Act
            var result = await _service.GetSupportedLanguagesAsync("whisper-1");

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains("en", result);
            Assert.Contains("es", result);
            Assert.Contains("fr", result);
            Assert.Contains("de", result);
        }

        [Fact]
        public async Task GetSupportedFormatsAsync_ForTTS_ReturnsFormats()
        {
            // Act
            var result = await _service.GetSupportedFormatsAsync("tts-1");

            // Assert
            Assert.Equal(4, result.Count);
            Assert.Contains("mp3", result);
            Assert.Contains("opus", result);
            Assert.Contains("aac", result);
            Assert.Contains("flac", result);
        }

        [Theory]
        [InlineData("openai", "chat", "gpt-4o")]
        [InlineData("openai", "transcription", "whisper-1")]
        [InlineData("openai", "tts", "tts-1")]
        [InlineData("openai", "realtime", "gpt-4o-realtime-preview")]
        [InlineData("anthropic", "chat", "claude-3-5-sonnet-20241022")]
        [InlineData("gemini", "chat", "gemini-1.5-pro")]
        [InlineData("elevenlabs", "tts", "eleven_multilingual_v2")]
        [InlineData("ultravox", "realtime", "ultravox-v0_2")]
        public async Task GetDefaultModelAsync_ReturnsExpectedDefault(string provider, string capabilityType, string expectedModel)
        {
            // Act
            var result = await _service.GetDefaultModelAsync(provider, capabilityType);

            // Assert
            Assert.Equal(expectedModel, result);
        }

        [Fact]
        public async Task GetDefaultModelAsync_ReturnsNullForUnknown()
        {
            // Act
            var result = await _service.GetDefaultModelAsync("unknown", "unknown");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RefreshCacheAsync_LogsMessage()
        {
            // Act
            await _service.RefreshCacheAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Model capability cache refresh requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}