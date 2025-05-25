using System;
using System.Text.Json;
using Xunit;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Tests.Core.Models.Audio
{
    public class AudioSerializationTests
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        [Fact]
        public void AudioTranscriptionRequest_SerializesCorrectly()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                AudioData = new byte[] { 1, 2, 3 },
                Model = "whisper-1",
                Language = "en",
                Temperature = 0.5,
                ResponseFormat = TranscriptionFormat.Json,
                TimestampGranularity = TimestampGranularity.Word
            };

            // Act
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<AudioTranscriptionRequest>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(request.AudioData, deserialized!.AudioData);
            Assert.Equal(request.Model, deserialized.Model);
            Assert.Equal(request.Language, deserialized.Language);
            Assert.Equal(request.Temperature, deserialized.Temperature);
            Assert.Equal(request.ResponseFormat, deserialized.ResponseFormat);
            Assert.Equal(request.TimestampGranularity, deserialized.TimestampGranularity);
        }

        [Fact]
        public void AudioTranscriptionResponse_SerializesCorrectly()
        {
            // Arrange
            var response = new AudioTranscriptionResponse
            {
                Text = "Hello, world!",
                Language = "en",
                Duration = 5.5,
                Confidence = 0.95,
                Segments = new List<TranscriptionSegment>
                {
                    new TranscriptionSegment
                    {
                        Id = 1,
                        Start = 0.0,
                        End = 2.5,
                        Text = "Hello,",
                        Confidence = 0.98
                    }
                },
                Words = new List<TranscriptionWord>
                {
                    new TranscriptionWord
                    {
                        Word = "Hello",
                        Start = 0.0,
                        End = 0.8,
                        Confidence = 0.99
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<AudioTranscriptionResponse>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(response.Text, deserialized!.Text);
            Assert.Equal(response.Language, deserialized.Language);
            Assert.Equal(response.Duration, deserialized.Duration);
            Assert.Equal(response.Confidence, deserialized.Confidence);
            Assert.NotNull(deserialized.Segments);
            Assert.Single(deserialized.Segments);
            Assert.Equal("Hello,", deserialized.Segments[0].Text);
            Assert.NotNull(deserialized.Words);
            Assert.Single(deserialized.Words);
            Assert.Equal("Hello", deserialized.Words[0].Word);
        }

        [Fact]
        public void TextToSpeechResponse_SerializesCorrectly()
        {
            // Arrange
            var response = new TextToSpeechResponse
            {
                AudioData = new byte[] { 255, 251, 144, 100 },
                Format = "mp3",
                SampleRate = 24000,
                Duration = 3.5,
                Channels = 1,
                BitDepth = 16,
                CharacterCount = 50,
                VoiceUsed = "alloy",
                ModelUsed = "tts-1"
            };

            // Act
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<TextToSpeechResponse>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(response.AudioData, deserialized!.AudioData);
            Assert.Equal(response.Format, deserialized.Format);
            Assert.Equal(response.SampleRate, deserialized.SampleRate);
            Assert.Equal(response.Duration, deserialized.Duration);
            Assert.Equal(response.Channels, deserialized.Channels);
            Assert.Equal(response.BitDepth, deserialized.BitDepth);
            Assert.Equal(response.CharacterCount, deserialized.CharacterCount);
            Assert.Equal(response.VoiceUsed, deserialized.VoiceUsed);
            Assert.Equal(response.ModelUsed, deserialized.ModelUsed);
        }

        [Fact]
        public void RealtimeMessage_SerializesCorrectly()
        {
            // Arrange
            var audioFrame = new RealtimeAudioFrame
            {
                SessionId = "session-123",
                AudioData = new byte[] { 1, 2, 3 },
                SampleRate = 16000,
                Channels = 1,
                DurationMs = 100.5
            };

            // Act
            var json = JsonSerializer.Serialize(audioFrame, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<RealtimeAudioFrame>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(audioFrame.SessionId, deserialized!.SessionId);
            Assert.Equal(audioFrame.AudioData, deserialized.AudioData);
            Assert.Equal(audioFrame.SampleRate, deserialized.SampleRate);
            Assert.Equal(audioFrame.Channels, deserialized.Channels);
            Assert.Equal(audioFrame.DurationMs, deserialized.DurationMs);
        }

        [Fact]
        public void RealtimeResponse_WithToolCall_SerializesCorrectly()
        {
            // Arrange
            var response = new RealtimeResponse
            {
                EventType = RealtimeEventType.ToolCallRequest,
                SessionId = "session-456",
                ToolCall = new RealtimeToolCall
                {
                    CallId = "call-789",
                    FunctionName = "get_weather",
                    Arguments = "{\"location\": \"Seattle\"}",
                    Type = "function"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<RealtimeResponse>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(response.EventType, deserialized!.EventType);
            Assert.Equal(response.SessionId, deserialized.SessionId);
            Assert.NotNull(deserialized.ToolCall);
            Assert.Equal(response.ToolCall.CallId, deserialized.ToolCall.CallId);
            Assert.Equal(response.ToolCall.FunctionName, deserialized.ToolCall.FunctionName);
            Assert.Equal(response.ToolCall.Arguments, deserialized.ToolCall.Arguments);
        }

        [Fact]
        public void VoiceInfo_SerializesCorrectly()
        {
            // Arrange
            var voiceInfo = new VoiceInfo
            {
                VoiceId = "voice-123",
                Name = "Emma",
                Description = "Friendly female voice",
                Gender = VoiceGender.Female,
                Age = VoiceAge.YoungAdult,
                SupportedLanguages = new List<string> { "en", "es", "fr" },
                Accent = "British",
                SupportedStyles = new List<string> { "cheerful", "serious" },
                IsPremium = true,
                IsCustom = false,
                SampleUrl = "https://example.com/sample.mp3"
            };

            // Act
            var json = JsonSerializer.Serialize(voiceInfo, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<VoiceInfo>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(voiceInfo.VoiceId, deserialized!.VoiceId);
            Assert.Equal(voiceInfo.Name, deserialized.Name);
            Assert.Equal(voiceInfo.Description, deserialized.Description);
            Assert.Equal(voiceInfo.Gender, deserialized.Gender);
            Assert.Equal(voiceInfo.Age, deserialized.Age);
            Assert.Equal(voiceInfo.SupportedLanguages, deserialized.SupportedLanguages);
            Assert.Equal(voiceInfo.Accent, deserialized.Accent);
            Assert.Equal(voiceInfo.SupportedStyles, deserialized.SupportedStyles);
            Assert.Equal(voiceInfo.IsPremium, deserialized.IsPremium);
            Assert.Equal(voiceInfo.IsCustom, deserialized.IsCustom);
            Assert.Equal(voiceInfo.SampleUrl, deserialized.SampleUrl);
        }

        [Fact]
        public void AudioProviderCapabilities_SerializesCorrectly()
        {
            // Arrange
            var capabilities = new AudioProviderCapabilities
            {
                Provider = "openai",
                DisplayName = "OpenAI",
                SupportedCapabilities = new List<AudioCapability>
                {
                    AudioCapability.BasicTranscription,
                    AudioCapability.BasicTTS,
                    AudioCapability.RealtimeConversation
                },
                Quality = new QualityRatings
                {
                    TranscriptionAccuracy = 95,
                    TTSNaturalness = 90,
                    RealtimeLatency = 20,
                    Reliability = 98
                }
            };

            // Act
            var json = JsonSerializer.Serialize(capabilities, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<AudioProviderCapabilities>(json, _jsonOptions);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(capabilities.Provider, deserialized!.Provider);
            Assert.Equal(capabilities.DisplayName, deserialized.DisplayName);
            Assert.Equal(capabilities.SupportedCapabilities.Count, deserialized.SupportedCapabilities.Count);
            Assert.NotNull(deserialized.Quality);
            Assert.Equal(capabilities.Quality.TranscriptionAccuracy, deserialized.Quality.TranscriptionAccuracy);
        }
    }
}