using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;
using Moq;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for the Audio API functionality.
    /// These tests demonstrate end-to-end usage of audio transcription, TTS, and real-time APIs.
    /// </summary>
    public class AudioApiIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ServiceProvider _serviceProvider;
        private readonly IConduit _conduit;
        private readonly ILogger<AudioApiIntegrationTests> _logger;

        public AudioApiIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Set up service collection with test configuration
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            // Add core services
            services.AddMemoryCache();
            
            // Add ConduitRegistry
            services.AddSingleton<ConduitRegistry>();
            
            // Add router and factory
            services.AddSingleton<ILLMRouter, DefaultLLMRouter>();
            services.AddSingleton<ILLMClientFactory, DefaultLLMClientFactory>();
            services.AddSingleton<IModelCapabilityDetector, ModelCapabilityDetector>();
            
            // Add Conduit
            services.AddSingleton<IConduit, Conduit>();
            
            // Add configuration services - this doesn't exist yet, so we'll mock it
            var mockMappingService = new Mock<ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService>();
            
            // Setup model mappings for audio models
            var whisperMapping = new ConduitLLM.Core.Interfaces.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "whisper-1", 
                ProviderName = "openai", 
                ProviderModelId = "whisper-1",
                IsEnabled = true
            };
            var ttsMapping = new ConduitLLM.Core.Interfaces.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "tts-1", 
                ProviderName = "openai", 
                ProviderModelId = "tts-1",
                IsEnabled = true
            };
            var realtimeMapping = new ConduitLLM.Core.Interfaces.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4o-realtime", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4o-realtime-preview-2024-12-17",
                IsEnabled = true
            };
            
            mockMappingService.Setup(x => x.GetMappingByModelAliasAsync("whisper-1"))
                .ReturnsAsync(whisperMapping);
            mockMappingService.Setup(x => x.GetMappingByModelAliasAsync("tts-1"))
                .ReturnsAsync(ttsMapping);
            mockMappingService.Setup(x => x.GetMappingByModelAliasAsync("gpt-4o-realtime"))
                .ReturnsAsync(realtimeMapping);
                
            services.AddSingleton<ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService>(
                mockMappingService.Object);
            services.AddSingleton<ConduitLLM.Core.Interfaces.IVirtualKeyService>(
                new Mock<ConduitLLM.Core.Interfaces.IVirtualKeyService>().Object);
            services.AddSingleton<ConduitLLM.Core.Interfaces.ICostCalculationService, CostCalculationService>();
            
            // Add mock provider credentials
            var mockCredentialService = new Mock<ConduitLLM.Core.Interfaces.Configuration.IProviderCredentialService>();
            mockCredentialService.Setup(x => x.GetCredentialByProviderNameAsync("openai"))
                .ReturnsAsync(new ConduitLLM.Core.Interfaces.Configuration.ProviderCredentials
                { 
                    ProviderName = "openai",
                    ApiKey = "test-api-key",
                    BaseUrl = "https://api.openai.com/v1"
                });
            services.AddSingleton(mockCredentialService.Object);
            
            _serviceProvider = services.BuildServiceProvider();
            _conduit = _serviceProvider.GetRequiredService<IConduit>();
            _logger = _serviceProvider.GetRequiredService<ILogger<AudioApiIntegrationTests>>();
        }

        [Fact(Skip = "Requires actual API credentials")]
        public async Task TranscribeAudio_WithValidFile_ReturnsTranscription()
        {
            // Arrange
            var audioData = GenerateTestAudioData();
            var client = _conduit.GetClient("whisper-1");
            
            // Act
            if (client is IAudioTranscriptionClient transcriptionClient)
            {
                var request = new AudioTranscriptionRequest
                {
                    AudioData = audioData,
                    Language = "en",
                    ResponseFormat = TranscriptionFormat.Text
                };
                
                var response = await transcriptionClient.TranscribeAudioAsync(request);
                
                // Assert
                Assert.NotNull(response);
                Assert.NotEmpty(response.Text);
                _output.WriteLine($"Transcription: {response.Text}");
            }
            else
            {
                throw new InvalidOperationException("Client does not support audio transcription");
            }
        }

        [Fact(Skip = "Requires actual API credentials")]
        public async Task TextToSpeech_WithValidText_ReturnsAudio()
        {
            // Arrange
            var text = "Hello, this is a test of the text-to-speech functionality.";
            var client = _conduit.GetClient("tts-1");
            
            // Act
            if (client is ITextToSpeechClient ttsClient)
            {
                var request = new TextToSpeechRequest
                {
                    Input = text,
                    Voice = "alloy",
                    ResponseFormat = AudioFormat.Mp3
                };
                
                var response = await ttsClient.CreateSpeechAsync(request);
                
                // Assert
                Assert.NotNull(response);
                Assert.NotNull(response.AudioData);
                Assert.True(response.AudioData.Length > 0);
                Assert.Equal(AudioFormat.Mp3.ToString(), response.Format?.ToString());
                _output.WriteLine($"Generated audio size: {response.AudioData.Length} bytes");
            }
            else
            {
                throw new InvalidOperationException("Client does not support text-to-speech");
            }
        }

        [Fact(Skip = "Requires actual API credentials and WebSocket support")]
        public async Task RealtimeAudio_WithValidSession_ExchangesMessages()
        {
            // Arrange
            var client = _conduit.GetClient("gpt-4o-realtime");
            
            // Act
            if (client is IRealtimeAudioClient realtimeClient)
            {
                var sessionConfig = new RealtimeSessionConfig
                {
                    Model = "gpt-4o-realtime-preview-2024-12-17",
                    Voice = "alloy",
                    SystemPrompt = "You are a helpful assistant. Respond concisely.",
                    InputFormat = RealtimeAudioFormat.PCM16_24kHz,
                    OutputFormat = RealtimeAudioFormat.PCM16_24kHz,
                    TurnDetection = new TurnDetectionConfig
                    {
                        Type = TurnDetectionType.ServerVAD,
                        Threshold = 0.5,
                        SilenceThresholdMs = 500
                    }
                };
                
                var session = await realtimeClient.CreateSessionAsync(sessionConfig);
                Assert.NotNull(session);
                
                // Note: Clean up would be done by closing the session
                // await session.CloseAsync(); // Not implemented in current API
            }
            else
            {
                throw new InvalidOperationException("Client does not support realtime audio");
            }
        }

        [Fact(Skip = "Integration test requires full provider registration")]
        public async Task AudioRouter_SelectsCorrectProvider_ForDifferentCapabilities()
        {
            // This test verifies that the router correctly selects providers based on audio capabilities
            
            // Test transcription routing
            var transcriptionClient = _conduit.GetClient("whisper-1");
            Assert.NotNull(transcriptionClient);
            Assert.True(transcriptionClient is IAudioTranscriptionClient);
            
            // Test TTS routing
            var ttsClient = _conduit.GetClient("tts-1");
            Assert.NotNull(ttsClient);
            Assert.True(ttsClient is ITextToSpeechClient);
            
            // Test realtime routing
            var realtimeClient = _conduit.GetClient("gpt-4o-realtime");
            Assert.NotNull(realtimeClient);
            Assert.True(realtimeClient is IRealtimeAudioClient);
            
            await Task.CompletedTask;
        }

        private byte[] GenerateTestAudioData()
        {
            // Generate a simple WAV header and silent PCM data for testing
            var sampleRate = 16000;
            var bitsPerSample = 16;
            var channels = 1;
            var duration = 1; // 1 second
            var dataSize = sampleRate * channels * (bitsPerSample / 8) * duration;
            
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // WAV header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize); // File size
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk size
                writer.Write((short)1); // Audio format (PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * (bitsPerSample / 8)); // Byte rate
                writer.Write((short)(channels * (bitsPerSample / 8))); // Block align
                writer.Write((short)bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                
                // Write silent audio data
                for (int i = 0; i < dataSize / 2; i++)
                {
                    writer.Write((short)0);
                }
                
                return stream.ToArray();
            }
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}