using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Providers
{
    public class RealtimeTranslatorTests
    {
        private readonly Mock<ILogger<OpenAIRealtimeTranslatorV2>> _mockOpenAILogger;
        private readonly Mock<ILogger<UltravoxRealtimeTranslator>> _mockUltravoxLogger;
        private readonly Mock<ILogger<ElevenLabsRealtimeTranslator>> _mockElevenLabsLogger;

        public RealtimeTranslatorTests()
        {
            _mockOpenAILogger = new Mock<ILogger<OpenAIRealtimeTranslatorV2>>();
            _mockUltravoxLogger = new Mock<ILogger<UltravoxRealtimeTranslator>>();
            _mockElevenLabsLogger = new Mock<ILogger<ElevenLabsRealtimeTranslator>>();
        }

        [Fact]
        public void OpenAITranslator_Should_Have_Correct_Provider_Name()
        {
            var translator = new OpenAIRealtimeTranslatorV2(_mockOpenAILogger.Object);
            Assert.Equal("OpenAI", translator.Provider);
        }

        [Fact]
        public void UltravoxTranslator_Should_Have_Correct_Provider_Name()
        {
            var translator = new UltravoxRealtimeTranslator(_mockUltravoxLogger.Object);
            Assert.Equal("Ultravox", translator.Provider);
        }

        [Fact]
        public void ElevenLabsTranslator_Should_Have_Correct_Provider_Name()
        {
            var translator = new ElevenLabsRealtimeTranslator(_mockElevenLabsLogger.Object);
            Assert.Equal("ElevenLabs", translator.Provider);
        }

        [Fact]
        public async Task OpenAI_TranslateToProvider_Should_Handle_AudioFrame()
        {
            // Arrange
            var translator = new OpenAIRealtimeTranslatorV2(_mockOpenAILogger.Object);
            var audioFrame = new RealtimeAudioFrame
            {
                AudioData = new byte[] { 1, 2, 3, 4 },
                IsOutput = false
            };

            // Act
            var result = await translator.TranslateToProviderAsync(audioFrame);

            // Assert
            Assert.NotNull(result);
            var json = JsonDocument.Parse(result);
            Assert.Equal("input_audio_buffer.append", json.RootElement.GetProperty("type").GetString());
            Assert.True(json.RootElement.TryGetProperty("audio", out _));
        }

        [Fact]
        public async Task Ultravox_TranslateToProvider_Should_Handle_TextInput()
        {
            // Arrange
            var translator = new UltravoxRealtimeTranslator(_mockUltravoxLogger.Object);
            var textInput = new RealtimeTextInput
            {
                Text = "Hello, world!"
            };

            // Act
            var result = await translator.TranslateToProviderAsync(textInput);

            // Assert
            Assert.NotNull(result);
            var json = JsonDocument.Parse(result);
            Assert.Equal("text", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("Hello, world!", json.RootElement.GetProperty("data").GetProperty("text").GetString());
        }

        [Fact]
        public async Task ElevenLabs_TranslateToProvider_Should_Handle_AudioFrame()
        {
            // Arrange
            var translator = new ElevenLabsRealtimeTranslator(_mockElevenLabsLogger.Object);
            var audioFrame = new RealtimeAudioFrame
            {
                AudioData = new byte[] { 1, 2, 3, 4 },
                IsOutput = false
            };

            // Act
            var result = await translator.TranslateToProviderAsync(audioFrame);

            // Assert
            Assert.NotNull(result);
            var json = JsonDocument.Parse(result);
            Assert.Equal("audio_input", json.RootElement.GetProperty("type").GetString());
            Assert.True(json.RootElement.TryGetProperty("audio", out var audio));
            Assert.Equal("pcm", audio.GetProperty("format").GetString());
        }

        [Fact]
        public async Task OpenAI_TranslateFromProvider_Should_Handle_AudioDelta()
        {
            // Arrange
            var translator = new OpenAIRealtimeTranslatorV2(_mockOpenAILogger.Object);
            var providerMessage = @"{
                ""type"": ""response.audio.delta"",
                ""delta"": ""SGVsbG8=""
            }";

            // Act
            var messages = await translator.TranslateFromProviderAsync(providerMessage);

            // Assert
            Assert.NotNull(messages);
            var messageList = messages.ToList();
            Assert.Single(messageList);
            Assert.IsType<RealtimeAudioFrame>(messageList[0]);
            var audioFrame = (RealtimeAudioFrame)messageList[0];
            Assert.True(audioFrame.IsOutput);
        }

        [Fact]
        public async Task Ultravox_ValidateSessionConfig_Should_Accept_Valid_Config()
        {
            // Arrange
            var translator = new UltravoxRealtimeTranslator(_mockUltravoxLogger.Object);
            var config = new RealtimeSessionConfig
            {
                Model = "ultravox-v2",
                InputFormat = RealtimeAudioFormat.PCM16_24kHz,
                TurnDetection = new TurnDetectionConfig
                {
                    Enabled = true,
                    Type = TurnDetectionType.ServerVAD
                }
            };

            // Act
            var result = await translator.ValidateSessionConfigAsync(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ElevenLabs_ValidateSessionConfig_Should_Warn_About_Unknown_Voice()
        {
            // Arrange
            var translator = new ElevenLabsRealtimeTranslator(_mockElevenLabsLogger.Object);
            var config = new RealtimeSessionConfig
            {
                Voice = "unknown-voice",
                InputFormat = RealtimeAudioFormat.PCM16_16kHz
            };

            // Act
            var result = await translator.ValidateSessionConfigAsync(config);

            // Assert
            Assert.True(result.IsValid); // Warning doesn't make it invalid
            Assert.NotEmpty(result.Warnings);
            Assert.Contains("unknown-voice", result.Warnings[0]);
        }

        [Fact]
        public void OpenAI_GetRequiredSubprotocol_Should_Return_Correct_Value()
        {
            // Arrange
            var translator = new OpenAIRealtimeTranslatorV2(_mockOpenAILogger.Object);

            // Act
            var subprotocol = translator.GetRequiredSubprotocol();

            // Assert
            Assert.Equal("openai-beta.realtime-v1", subprotocol);
        }

        [Fact]
        public async Task Ultravox_GetConnectionHeaders_Should_Include_Version()
        {
            // Arrange
            var translator = new UltravoxRealtimeTranslator(_mockUltravoxLogger.Object);
            var config = new RealtimeSessionConfig();

            // Act
            var headers = await translator.GetConnectionHeadersAsync(config);

            // Assert
            Assert.NotNull(headers);
            Assert.Contains("X-Ultravox-Version", headers.Keys);
            Assert.Equal("1.0", headers["X-Ultravox-Version"]);
        }

        [Fact]
        public async Task ElevenLabs_TransformSessionConfig_Should_Include_VoiceSettings()
        {
            // Arrange
            var translator = new ElevenLabsRealtimeTranslator(_mockElevenLabsLogger.Object);
            var config = new RealtimeSessionConfig
            {
                Voice = "rachel",
                Temperature = 0.9
            };

            // Act
            var result = await translator.TransformSessionConfigAsync(config);

            // Assert
            Assert.NotNull(result);
            var json = JsonDocument.Parse(result);
            var configElement = json.RootElement.GetProperty("config");
            Assert.True(configElement.TryGetProperty("voice_settings", out var voiceSettings));
            Assert.Equal(0.5, voiceSettings.GetProperty("stability").GetDouble());
        }

        [Fact]
        public void TranslatorFactory_Should_Create_OpenAI_Translator()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockLogger = new Mock<ILogger<RealtimeMessageTranslatorFactory>>();
            var mockTranslatorLogger = new Mock<ILogger<OpenAIRealtimeTranslatorV2>>();

            mockServiceProvider.Setup(sp => sp.GetService(typeof(ILogger<OpenAIRealtimeTranslatorV2>)))
                .Returns(mockTranslatorLogger.Object);

            var factory = new RealtimeMessageTranslatorFactory(mockServiceProvider.Object, mockLogger.Object);

            // Act
            var translator = factory.GetTranslator("openai");

            // Assert
            Assert.NotNull(translator);
            Assert.Equal("OpenAI", translator.Provider);
        }

        [Fact]
        public void TranslatorFactory_Should_Support_Case_Insensitive_Provider_Names()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockLogger = new Mock<ILogger<RealtimeMessageTranslatorFactory>>();
            var mockTranslatorLogger = new Mock<ILogger<UltravoxRealtimeTranslator>>();

            mockServiceProvider.Setup(sp => sp.GetService(typeof(ILogger<UltravoxRealtimeTranslator>)))
                .Returns(mockTranslatorLogger.Object);

            var factory = new RealtimeMessageTranslatorFactory(mockServiceProvider.Object, mockLogger.Object);

            // Act
            var translator1 = factory.GetTranslator("ULTRAVOX");
            var translator2 = factory.GetTranslator("ultravox");

            // Assert
            Assert.NotNull(translator1);
            Assert.NotNull(translator2);
            Assert.Same(translator1, translator2); // Should be cached
        }

        [Fact]
        public void TranslatorFactory_Should_Return_Null_For_Unknown_Provider()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockLogger = new Mock<ILogger<RealtimeMessageTranslatorFactory>>();
            var factory = new RealtimeMessageTranslatorFactory(mockServiceProvider.Object, mockLogger.Object);

            // Act
            var translator = factory.GetTranslator("unknown-provider");

            // Assert
            Assert.Null(translator);
        }
    }
}
