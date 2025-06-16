using System;

using ConduitLLM.Core.Models.Audio;

using Xunit;

namespace ConduitLLM.Tests.Core.Models.Audio
{
    public class AudioTranscriptionRequestTests
    {
        [Fact]
        public void IsValid_WithAudioData_ReturnsTrue()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioData = new byte[] { 1, 2, 3, 4, 5 },
                Model = "whisper-1"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void IsValid_WithAudioUrl_ReturnsTrue()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioUrl = "https://example.com/audio.mp3",
                Model = "whisper-1"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void IsValid_WithBothAudioDataAndUrl_ReturnsFalse()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioData = new byte[] { 1, 2, 3 },
                AudioUrl = "https://example.com/audio.mp3"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("Only one of AudioData or AudioUrl should be provided", errorMessage);
        }

        [Fact]
        public void IsValid_WithoutAudioDataOrUrl_ReturnsFalse()
        {
            // Arrange
            var request = new AudioTranscriptionRequest();

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("Either AudioData or AudioUrl must be provided", errorMessage);
        }

        [Fact]
        public void IsValid_WithEmptyAudioData_ReturnsFalse()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioData = Array.Empty<byte>()
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("AudioData cannot be empty", errorMessage);
        }

        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Arrange & Act
            var request = new AudioTranscriptionRequest();

            // Assert
            Assert.Null(request.AudioData);
            Assert.Null(request.AudioUrl);
            Assert.Null(request.Model);
            Assert.Null(request.Language);
            Assert.Null(request.Prompt);
            Assert.Null(request.Temperature);
            Assert.Null(request.ResponseFormat);
            Assert.Null(request.TimestampGranularity);
            Assert.True(request.IncludePunctuation);
            Assert.Null(request.FilterProfanity);
        }

        [Fact]
        public void LanguageValidation_WithValidISO639_1_Passes()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioData = new byte[] { 1, 2, 3 },
                Language = "en"
            };

            // Act & Assert - no exception thrown
            var isValid = request.IsValid(out _);
            Assert.True(isValid);
        }

        [Fact]
        public void ProviderOptions_CanBeSet()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioData = new byte[] { 1, 2, 3 },
                ProviderOptions = new Dictionary<string, object>
                {
                    ["custom_param"] = "value",
                    ["another_param"] = 123
                }
            };

            // Act & Assert
            Assert.NotNull(request.ProviderOptions);
            Assert.Equal(2, request.ProviderOptions.Count);
            Assert.Equal("value", request.ProviderOptions["custom_param"]);
            Assert.Equal(123, request.ProviderOptions["another_param"]);
        }
    }
}
