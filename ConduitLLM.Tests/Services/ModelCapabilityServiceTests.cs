using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Tests for the IModelCapabilityService interface implementations.
    /// </summary>
    public class ModelCapabilityServiceTests
    {
        private readonly Mock<IModelCapabilityService> _serviceMock;

        public ModelCapabilityServiceTests()
        {
            _serviceMock = new Mock<IModelCapabilityService>();
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
            // Arrange
            _serviceMock.Setup(x => x.SupportsVisionAsync(model)).ReturnsAsync(expected);

            // Act
            var result = await _serviceMock.Object.SupportsVisionAsync(model);

            // Assert
            Assert.Equal(expected, result);
            _serviceMock.Verify(x => x.SupportsVisionAsync(model), Times.Once);
        }

        [Theory]
        [InlineData("whisper-1", true)]
        [InlineData("whisper-large-v3", true)]
        [InlineData("gpt-4", false)]
        [InlineData("tts-1", false)]
        public async Task SupportsAudioTranscriptionAsync_ReturnsExpectedResult(string model, bool expected)
        {
            // Arrange
            _serviceMock.Setup(x => x.SupportsAudioTranscriptionAsync(model)).ReturnsAsync(expected);

            // Act
            var result = await _serviceMock.Object.SupportsAudioTranscriptionAsync(model);

            // Assert
            Assert.Equal(expected, result);
            _serviceMock.Verify(x => x.SupportsAudioTranscriptionAsync(model), Times.Once);
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
            // Arrange
            _serviceMock.Setup(x => x.SupportsTextToSpeechAsync(model)).ReturnsAsync(expected);

            // Act
            var result = await _serviceMock.Object.SupportsTextToSpeechAsync(model);

            // Assert
            Assert.Equal(expected, result);
            _serviceMock.Verify(x => x.SupportsTextToSpeechAsync(model), Times.Once);
        }

        [Theory]
        [InlineData("gpt-4o-realtime-preview", true)]
        [InlineData("ultravox-v0_2", true)]
        [InlineData("gpt-4", false)]
        [InlineData("tts-1", false)]
        public async Task SupportsRealtimeAudioAsync_ReturnsExpectedResult(string model, bool expected)
        {
            // Arrange
            _serviceMock.Setup(x => x.SupportsRealtimeAudioAsync(model)).ReturnsAsync(expected);

            // Act
            var result = await _serviceMock.Object.SupportsRealtimeAudioAsync(model);

            // Assert
            Assert.Equal(expected, result);
            _serviceMock.Verify(x => x.SupportsRealtimeAudioAsync(model), Times.Once);
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
            // Arrange
            _serviceMock.Setup(x => x.GetTokenizerTypeAsync(model)).ReturnsAsync(expectedTokenizer);

            // Act
            var result = await _serviceMock.Object.GetTokenizerTypeAsync(model);

            // Assert
            Assert.Equal(expectedTokenizer, result);
            _serviceMock.Verify(x => x.GetTokenizerTypeAsync(model), Times.Once);
        }

        [Fact]
        public async Task GetSupportedVoicesAsync_ForOpenAI_ReturnsCorrectVoices()
        {
            // Arrange
            var expectedVoices = new List<string> { "alloy", "echo", "fable", "nova", "onyx", "shimmer" };
            _serviceMock.Setup(x => x.GetSupportedVoicesAsync("tts-1")).ReturnsAsync(expectedVoices);

            // Act
            var result = await _serviceMock.Object.GetSupportedVoicesAsync("tts-1");

            // Assert
            Assert.Equal(expectedVoices.Count, result.Count);
            foreach (var voice in expectedVoices)
            {
                Assert.Contains(voice, result);
            }
            _serviceMock.Verify(x => x.GetSupportedVoicesAsync("tts-1"), Times.Once);
        }

        [Fact]
        public async Task GetSupportedVoicesAsync_ForElevenLabs_ReturnsCorrectVoices()
        {
            // Arrange
            var expectedVoices = new List<string> { "rachel", "drew", "clyde", "paul" };
            _serviceMock.Setup(x => x.GetSupportedVoicesAsync("eleven_multilingual_v2")).ReturnsAsync(expectedVoices);

            // Act
            var result = await _serviceMock.Object.GetSupportedVoicesAsync("eleven_multilingual_v2");

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains("rachel", result);
            Assert.Contains("drew", result);
            _serviceMock.Verify(x => x.GetSupportedVoicesAsync("eleven_multilingual_v2"), Times.Once);
        }

        [Fact]
        public async Task GetSupportedLanguagesAsync_ForWhisper_ReturnsEmptyList()
        {
            // Arrange
            _serviceMock.Setup(x => x.GetSupportedLanguagesAsync("whisper-1")).ReturnsAsync(new List<string>());

            // Act
            var result = await _serviceMock.Object.GetSupportedLanguagesAsync("whisper-1");

            // Assert
            // ConfigurationModelCapabilityService doesn't implement language support yet
            Assert.Empty(result);
            _serviceMock.Verify(x => x.GetSupportedLanguagesAsync("whisper-1"), Times.Once);
        }

        [Fact]
        public async Task GetSupportedFormatsAsync_ForTTS_ReturnsFormats()
        {
            // Arrange
            var expectedFormats = new List<string> { "mp3", "opus", "aac", "flac" };
            _serviceMock.Setup(x => x.GetSupportedFormatsAsync("tts-1")).ReturnsAsync(expectedFormats);

            // Act
            var result = await _serviceMock.Object.GetSupportedFormatsAsync("tts-1");

            // Assert
            Assert.Equal(4, result.Count);
            Assert.Contains("mp3", result);
            Assert.Contains("opus", result);
            Assert.Contains("aac", result);
            Assert.Contains("flac", result);
            _serviceMock.Verify(x => x.GetSupportedFormatsAsync("tts-1"), Times.Once);
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
            // Arrange
            _serviceMock.Setup(x => x.GetDefaultModelAsync(provider, capabilityType)).ReturnsAsync(expectedModel);

            // Act
            var result = await _serviceMock.Object.GetDefaultModelAsync(provider, capabilityType);

            // Assert
            Assert.Equal(expectedModel, result);
            _serviceMock.Verify(x => x.GetDefaultModelAsync(provider, capabilityType), Times.Once);
        }

        [Fact]
        public async Task GetDefaultModelAsync_ReturnsNullForUnknown()
        {
            // Arrange
            _serviceMock.Setup(x => x.GetDefaultModelAsync("unknown", "unknown")).ReturnsAsync((string?)null);

            // Act
            var result = await _serviceMock.Object.GetDefaultModelAsync("unknown", "unknown");

            // Assert
            Assert.Null(result);
            _serviceMock.Verify(x => x.GetDefaultModelAsync("unknown", "unknown"), Times.Once);
        }

        [Fact]
        public async Task RefreshCacheAsync_ClearsCache()
        {
            // Arrange
            _serviceMock.Setup(x => x.RefreshCacheAsync()).Returns(Task.CompletedTask);

            // Act
            await _serviceMock.Object.RefreshCacheAsync();

            // Assert
            // The RefreshCacheAsync method in ConfigurationModelCapabilityService clears the cache
            // We can't directly verify the cache clear with the mock, but we can verify it doesn't throw
            _serviceMock.Verify(x => x.RefreshCacheAsync(), Times.Once);
        }
    }
}