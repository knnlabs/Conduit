using System;
using System.Collections.Generic;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Xunit;

namespace ConduitLLM.Tests.Core.Models.Audio
{
    public class RealtimeSessionConfigTests
    {
        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Arrange & Act
            var config = new RealtimeSessionConfig();

            // Assert
            Assert.Null(config.Model);
            Assert.Equal(string.Empty, config.Voice);
            Assert.Equal(RealtimeAudioFormat.PCM16_24kHz, config.InputFormat);
            Assert.Equal(RealtimeAudioFormat.PCM16_24kHz, config.OutputFormat);
            Assert.Equal("en", config.Language);
            Assert.Null(config.SystemPrompt);
            Assert.NotNull(config.TurnDetection);
            Assert.Null(config.Tools);
            Assert.Null(config.Transcription);
            Assert.Null(config.VoiceSettings);
            Assert.Null(config.Temperature);
            Assert.Null(config.MaxResponseDurationSeconds);
            Assert.Equal(ConversationMode.Conversational, config.Mode);
            Assert.Null(config.ProviderConfig);
        }

        [Fact]
        public void TurnDetectionConfig_HasCorrectDefaults()
        {
            // Arrange & Act
            var turnDetection = new TurnDetectionConfig();

            // Assert
            Assert.True(turnDetection.Enabled);
            Assert.Equal(TurnDetectionType.ServerVAD, turnDetection.Type);
            Assert.Equal(500, turnDetection.SilenceThresholdMs);
            Assert.Null(turnDetection.Threshold);
            Assert.Equal(300, turnDetection.PrefixPaddingMs);
        }

        [Fact]
        public void TranscriptionConfig_CanBeConfigured()
        {
            // Arrange
            var config = new RealtimeSessionConfig
            {
                Transcription = new TranscriptionConfig
                {
                    EnableUserTranscription = true,
                    EnableAssistantTranscription = false,
                    IncludePartialTranscriptions = true,
                    TranscriptionModel = "whisper-1"
                }
            };

            // Act & Assert
            Assert.NotNull(config.Transcription);
            Assert.True(config.Transcription.EnableUserTranscription);
            Assert.False(config.Transcription.EnableAssistantTranscription);
            Assert.True(config.Transcription.IncludePartialTranscriptions);
            Assert.Equal("whisper-1", config.Transcription.TranscriptionModel);
        }

        [Fact]
        public void RealtimeVoiceSettings_CanBeConfigured()
        {
            // Arrange
            var voiceSettings = new RealtimeVoiceSettings
            {
                Speed = 1.2,
                Pitch = 0.8,
                Stability = 0.9,
                SimilarityBoost = 0.7,
                Style = "friendly",
                CustomSettings = new Dictionary<string, object>
                {
                    ["emotion"] = "happy"
                }
            };

            var config = new RealtimeSessionConfig
            {
                VoiceSettings = voiceSettings
            };

            // Act & Assert
            Assert.NotNull(config.VoiceSettings);
            Assert.Equal(1.2, config.VoiceSettings.Speed);
            Assert.Equal(0.8, config.VoiceSettings.Pitch);
            Assert.Equal(0.9, config.VoiceSettings.Stability);
            Assert.Equal(0.7, config.VoiceSettings.SimilarityBoost);
            Assert.Equal("friendly", config.VoiceSettings.Style);
            Assert.Equal("happy", config.VoiceSettings.CustomSettings["emotion"]);
        }

        [Fact]
        public void ToolsCanBeAdded()
        {
            // Arrange
            var config = new RealtimeSessionConfig
            {
                Tools = new List<Tool>
                {
                    new Tool
                    {
                        Type = "function",
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get weather information",
                            Parameters = System.Text.Json.JsonSerializer.SerializeToNode(new {
                                type = "object",
                                properties = new {
                                    location = new { type = "string" }
                                }
                            }) as System.Text.Json.Nodes.JsonObject
                        }
                    }
                }
            };

            // Act & Assert
            Assert.NotNull(config.Tools);
            Assert.Single(config.Tools);
            Assert.Equal("function", config.Tools[0].Type);
            Assert.Equal("get_weather", config.Tools[0].Function.Name);
        }

        [Fact]
        public void AllAudioFormats_AreDefined()
        {
            // Arrange & Act
            var formats = Enum.GetValues<RealtimeAudioFormat>();

            // Assert
            Assert.Contains(RealtimeAudioFormat.PCM16_8kHz, formats);
            Assert.Contains(RealtimeAudioFormat.PCM16_16kHz, formats);
            Assert.Contains(RealtimeAudioFormat.PCM16_24kHz, formats);
            Assert.Contains(RealtimeAudioFormat.PCM16_48kHz, formats);
            Assert.Contains(RealtimeAudioFormat.G711_ULAW, formats);
            Assert.Contains(RealtimeAudioFormat.G711_ALAW, formats);
            Assert.Contains(RealtimeAudioFormat.Opus, formats);
            Assert.Contains(RealtimeAudioFormat.MP3, formats);
        }

        [Fact]
        public void AllTurnDetectionTypes_AreDefined()
        {
            // Arrange & Act
            var types = Enum.GetValues<TurnDetectionType>();

            // Assert
            Assert.Contains(TurnDetectionType.ServerVAD, types);
            Assert.Contains(TurnDetectionType.Manual, types);
            Assert.Contains(TurnDetectionType.PushToTalk, types);
        }

        [Fact]
        public void AllConversationModes_AreDefined()
        {
            // Arrange & Act
            var modes = Enum.GetValues<ConversationMode>();

            // Assert
            Assert.Contains(ConversationMode.Conversational, modes);
            Assert.Contains(ConversationMode.Interview, modes);
            Assert.Contains(ConversationMode.Command, modes);
            Assert.Contains(ConversationMode.Presentation, modes);
            Assert.Contains(ConversationMode.Custom, modes);
        }

        [Fact]
        public void ProviderConfig_CanBeSet()
        {
            // Arrange
            var config = new RealtimeSessionConfig
            {
                ProviderConfig = new Dictionary<string, object>
                {
                    ["openai_specific"] = "value",
                    ["ultravox_param"] = 123
                }
            };

            // Act & Assert
            Assert.NotNull(config.ProviderConfig);
            Assert.Equal(2, config.ProviderConfig.Count);
            Assert.Equal("value", config.ProviderConfig["openai_specific"]);
            Assert.Equal(123, config.ProviderConfig["ultravox_param"]);
        }
    }
}
