using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.OpenAI;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Providers
{
    public partial class OpenAIClientTests
    {
        #region Audio Transcription Tests

        [Fact]
        public async Task TranscribeAudioAsync_WithValidRequest_ReturnsTranscription()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new AudioTranscriptionRequest
            {
                AudioData = Encoding.UTF8.GetBytes("fake audio data"),
                FileName = "test.mp3",
                Model = "whisper-1"
            };

            var expectedResponse = new TranscriptionResponse
            {
                Text = "This is the transcribed text",
                Language = "en",
                Duration = 10.5
            };

            SetupHttpResponse(HttpStatusCode.OK, expectedResponse);

            // Act
            var result = await client.TranscribeAudioAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResponse.Text, result.Text);
            Assert.Equal(expectedResponse.Language, result.Language);
            Assert.Equal(expectedResponse.Duration, result.Duration);
        }

        [Fact]
        public async Task TranscribeAudioAsync_WithLanguageAndPrompt_IncludesOptionalParameters()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new AudioTranscriptionRequest
            {
                AudioData = Encoding.UTF8.GetBytes("fake audio data"),
                FileName = "test.mp3",
                Model = "whisper-1",
                Language = "es",
                Prompt = "This is a conversation about technology",
                Temperature = 0.5
            };

            string? capturedContent = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    // Capture the content before returning
                    if (request.Content != null)
                    {
                        capturedContent = await request.Content.ReadAsStringAsync();
                    }
                    
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(new TranscriptionResponse
                        {
                            Text = "Transcribed text"
                        }))
                    };
                });

            // Act
            await client.TranscribeAudioAsync(request);

            // Assert
            Assert.NotNull(capturedContent);
            Assert.Contains("language", capturedContent);
            Assert.Contains("es", capturedContent);
            Assert.Contains("prompt", capturedContent);
            Assert.Contains("temperature", capturedContent);
        }

        [Fact]
        public async Task TranscribeAudioAsync_WithUrlInsteadOfData_ThrowsNotSupportedException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new AudioTranscriptionRequest
            {
                AudioUrl = "https://example.com/audio.mp3",
                Model = "whisper-1"
            };

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                client.TranscribeAudioAsync(request));
        }

        [Fact]
        public async Task TranscribeAudioAsync_WithDifferentResponseFormats_HandlesCorrectly()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new AudioTranscriptionRequest
            {
                AudioData = Encoding.UTF8.GetBytes("fake audio data"),
                FileName = "test.mp3",
                Model = "whisper-1",
                ResponseFormat = TranscriptionFormat.Text
            };

            SetupHttpResponse(HttpStatusCode.OK, "This is plain text response");

            // Act
            var result = await client.TranscribeAudioAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("This is plain text response", result.Text);
        }

        [Fact]
        public async Task TranscribeAudioAsync_WithApiError_ThrowsLLMCommunicationException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new AudioTranscriptionRequest
            {
                AudioData = Encoding.UTF8.GetBytes("fake audio data"),
                FileName = "test.mp3",
                Model = "whisper-1"
            };

            SetupHttpResponse(HttpStatusCode.BadRequest, new { error = new { message = "Invalid audio format" } });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.TranscribeAudioAsync(request));

            Assert.Contains("Audio transcription failed", exception.Message);
            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        }

        [Fact]
        public async Task TranscribeAudioAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var client = CreateOpenAIClient();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.TranscribeAudioAsync(null!));
        }

        [Fact]
        public async Task TranscribeAudioAsync_ForAzure_UsesCorrectEndpoint()
        {
            // Arrange
            var client = CreateAzureOpenAIClient();
            var request = new AudioTranscriptionRequest
            {
                AudioData = Encoding.UTF8.GetBytes("fake audio data"),
                FileName = "test.mp3",
                Model = "whisper-deployment"
            };

            string? capturedUrl = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    capturedUrl = request.RequestUri?.ToString();
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TranscriptionResponse
                    {
                        Text = "Transcribed text"
                    }))
                });

            // Act
            await client.TranscribeAudioAsync(request);

            // Assert
            Assert.NotNull(capturedUrl);
            Assert.Contains("/openai/deployments/", capturedUrl);
            Assert.Contains("/audio/transcriptions", capturedUrl);
            Assert.Contains("api-version=", capturedUrl);
        }

        #endregion

        #region Text-to-Speech Tests

        [Fact]
        public async Task CreateSpeechAsync_WithValidRequest_ReturnsAudioData()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new ConduitLLM.Core.Models.Audio.TextToSpeechRequest
            {
                Input = "Hello, this is a test",
                Voice = "alloy",
                Model = "tts-1"
            };

            var audioData = Encoding.UTF8.GetBytes("fake audio data");
            SetupHttpResponse(HttpStatusCode.OK, audioData, "audio/mpeg");

            // Act
            var result = await client.CreateSpeechAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(audioData, result.AudioData);
            Assert.Equal("alloy", result.VoiceUsed);
            Assert.Equal("tts-1", result.ModelUsed);
            Assert.Equal(request.Input.Length, result.CharacterCount);
        }

        [Fact]
        public async Task CreateSpeechAsync_WithDifferentFormats_HandlesCorrectly()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new ConduitLLM.Core.Models.Audio.TextToSpeechRequest
            {
                Input = "Test audio",
                Voice = "nova",
                Model = "tts-1",
                ResponseFormat = AudioFormat.Opus,
                Speed = 1.5
            };

            string? capturedContent = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    // Capture the content before returning
                    if (request.Content != null)
                    {
                        capturedContent = await request.Content.ReadAsStringAsync();
                    }
                    
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                    };
                });

            // Act
            await client.CreateSpeechAsync(request);

            // Assert
            Assert.NotNull(capturedContent);
            var json = JsonDocument.Parse(capturedContent);
            Assert.Equal("opus", json.RootElement.GetProperty("response_format").GetString());
            Assert.Equal(1.5, json.RootElement.GetProperty("speed").GetDouble());
        }

        [Fact]
        public async Task CreateSpeechAsync_WithApiError_ThrowsLLMCommunicationException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new ConduitLLM.Core.Models.Audio.TextToSpeechRequest
            {
                Input = "Test",
                Voice = "invalid-voice",
                Model = "tts-1"
            };

            SetupHttpResponse(HttpStatusCode.BadRequest, new { error = new { message = "Invalid voice" } });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.CreateSpeechAsync(request));

            Assert.Contains("Text-to-speech failed", exception.Message);
        }

        [Fact]
        public async Task StreamSpeechAsync_ReturnsChunkedAudio()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var request = new ConduitLLM.Core.Models.Audio.TextToSpeechRequest
            {
                Input = "Test streaming",
                Voice = "echo",
                Model = "tts-1"
            };

            var audioData = new byte[10000]; // Large enough to require multiple chunks
            Array.Fill(audioData, (byte)42);
            SetupHttpResponse(HttpStatusCode.OK, audioData, "audio/mpeg");

            // Act
            var chunks = new List<AudioChunk>();
            await foreach (var chunk in client.StreamSpeechAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.NotEmpty(chunks);
            Assert.True(chunks.Count > 1); // Should be chunked
            Assert.True(chunks.Last().IsFinal);
            
            // Verify data integrity
            var reconstructed = chunks.SelectMany(c => c.Data).ToArray();
            Assert.Equal(audioData.Length, reconstructed.Length);
        }

        #endregion

        #region Voice Listing Tests

        [Fact]
        public async Task ListVoicesAsync_ReturnsOpenAIVoices()
        {
            // Arrange
            var client = CreateOpenAIClient();

            // Act
            var voices = await client.ListVoicesAsync();

            // Assert
            Assert.NotNull(voices);
            Assert.Equal(6, voices.Count); // OpenAI has 6 voices
            Assert.Contains(voices, v => v.VoiceId == "alloy");
            Assert.Contains(voices, v => v.VoiceId == "echo");
            Assert.Contains(voices, v => v.VoiceId == "fable");
            Assert.Contains(voices, v => v.VoiceId == "onyx");
            Assert.Contains(voices, v => v.VoiceId == "nova");
            Assert.Contains(voices, v => v.VoiceId == "shimmer");
        }

        #endregion
    }
}