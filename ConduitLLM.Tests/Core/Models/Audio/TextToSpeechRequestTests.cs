using System;
using Xunit;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Tests.Core.Models.Audio
{
    public class TextToSpeechRequestTests
    {
        [Fact]
        public void IsValid_WithRequiredFields_ReturnsTrue()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Input = "Hello, world!",
                Voice = "alloy"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void IsValid_WithoutInput_ReturnsFalse()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Voice = "alloy"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("Input text is required", errorMessage);
        }

        [Fact]
        public void IsValid_WithEmptyInput_ReturnsFalse()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Input = "",
                Voice = "alloy"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("Input text is required", errorMessage);
        }

        [Fact]
        public void IsValid_WithoutVoice_ReturnsFalse()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Input = "Hello, world!"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("Voice selection is required", errorMessage);
        }

        [Fact]
        public void IsValid_WithTooLongInput_ReturnsFalse()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Input = new string('a', 10001),
                Voice = "alloy"
            };

            // Act
            var isValid = request.IsValid(out var errorMessage);

            // Assert
            Assert.False(isValid);
            Assert.Equal("Input text exceeds maximum length of 10000 characters", errorMessage);
        }

        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Arrange & Act
            var request = new TextToSpeechRequest();

            // Assert
            Assert.Equal(string.Empty, request.Input);
            Assert.Equal(string.Empty, request.Voice);
            Assert.Null(request.Model);
            Assert.Null(request.ResponseFormat);
            Assert.Null(request.Speed);
            Assert.Null(request.Pitch);
            Assert.Null(request.Volume);
            Assert.Null(request.VoiceSettings);
            Assert.Null(request.Language);
            Assert.Null(request.EnableSSML);
            Assert.Null(request.SampleRate);
            Assert.Null(request.OptimizeStreaming);
        }

        [Fact]
        public void SpeedRange_WithinBounds_IsValid()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Input = "Test",
                Voice = "alloy",
                Speed = 1.5
            };

            // Act
            var isValid = request.IsValid(out _);

            // Assert
            Assert.True(isValid);
            Assert.Equal(1.5, request.Speed);
        }

        [Fact]
        public void VoiceSettings_CanBeSet()
        {
            // Arrange
            var voiceSettings = new VoiceSettings
            {
                Emotion = 0.8,
                Style = "cheerful",
                Emphasis = 1.2,
                SimilarityBoost = 0.9,
                Stability = 0.7,
                CustomSettings = new Dictionary<string, object>
                {
                    ["custom"] = "value"
                }
            };

            var request = new TextToSpeechRequest
            {
                Input = "Test",
                Voice = "alloy",
                VoiceSettings = voiceSettings
            };

            // Act & Assert
            Assert.NotNull(request.VoiceSettings);
            Assert.Equal(0.8, request.VoiceSettings.Emotion);
            Assert.Equal("cheerful", request.VoiceSettings.Style);
            Assert.Equal(1.2, request.VoiceSettings.Emphasis);
            Assert.Equal(0.9, request.VoiceSettings.SimilarityBoost);
            Assert.Equal(0.7, request.VoiceSettings.Stability);
            Assert.NotNull(request.VoiceSettings.CustomSettings);
            Assert.Equal("value", request.VoiceSettings.CustomSettings["custom"]);
        }

        [Fact]
        public void AllAudioFormats_AreDefined()
        {
            // Arrange & Act
            var formats = Enum.GetValues<AudioFormat>();

            // Assert
            Assert.Contains(AudioFormat.Mp3, formats);
            Assert.Contains(AudioFormat.Wav, formats);
            Assert.Contains(AudioFormat.Flac, formats);
            Assert.Contains(AudioFormat.Ogg, formats);
            Assert.Contains(AudioFormat.Aac, formats);
            Assert.Contains(AudioFormat.Opus, formats);
            Assert.Contains(AudioFormat.Pcm, formats);
            Assert.Contains(AudioFormat.Ulaw, formats);
            Assert.Contains(AudioFormat.Alaw, formats);
        }
    }
}