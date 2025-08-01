using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers;
using ConduitLLM.Providers.Providers.OpenAI;
using ConduitLLM.Providers.Providers.OpenAI.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Tests.TestHelpers;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the OpenAIClient class, covering standard OpenAI and Azure OpenAI scenarios.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class OpenAIClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IModelCapabilityService> _capabilityServiceMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public OpenAIClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _capabilityServiceMock = new Mock<IModelCapabilityService>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidOpenAICredentials_InitializesCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var modelId = "gpt-4";
            var logger = CreateLogger<OpenAIClient>();

            // Act
            var client = new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithValidAzureCredentials_InitializesCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.AzureOpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key",
                BaseUrl = "https://myinstance.openai.azure.com"
            };
            
            var modelId = "my-deployment";
            var logger = CreateLogger<OpenAIClient>();

            // Act
            var client = new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithAzureButNoApiBase_ThrowsConfigurationException()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.AzureOpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
                // No BaseUrl set
            };
            
            var modelId = "my-deployment";
            var logger = CreateLogger<OpenAIClient>();

            // Act & Assert
            var exception = Assert.Throws<ConfigurationException>(() =>
                new OpenAIClient(
                    provider,
                    keyCredential,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object,
                    _capabilityServiceMock.Object));

            Assert.Contains("BaseUrl", exception.Message);
        }

        [Fact]
        public void Constructor_WithNullCredentials_ThrowsException()
        {
            // Arrange
            var modelId = "gpt-4";
            var logger = CreateLogger<OpenAIClient>();

            // Act & Assert
            // The constructor will throw either NullReferenceException or ArgumentNullException
            // depending on the order of validation
            Assert.ThrowsAny<Exception>(() =>
                new OpenAIClient(
                    null!,
                    null!,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var provider = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key"
            };
            
            var modelId = "gpt-4";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new OpenAIClient(
                    provider,
                    keyCredential,
                    modelId,
                    null!,
                    _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_DoesNotThrow()
        {
            // Arrange
            var provider = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key"
            };
            
            var modelId = "gpt-4";
            var logger = CreateLogger<OpenAIClient>();

            // Act
            var client = new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                null); // HttpClientFactory is optional

            // Assert
            Assert.NotNull(client);
        }

        #endregion

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

        #region Capability Tests

        [Fact]
        public async Task SupportsTranscriptionAsync_WithCapabilityService_UsesService()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsAudioTranscriptionAsync("gpt-4"))
                .ReturnsAsync(true);

            // Act
            var result = await client.SupportsTranscriptionAsync();

            // Assert
            Assert.True(result);
            _capabilityServiceMock.Verify(x => x.SupportsAudioTranscriptionAsync("gpt-4"), Times.Once);
        }

        [Fact]
        public async Task SupportsTranscriptionAsync_WithCapabilityServiceError_FallsBackToDefault()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsAudioTranscriptionAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await client.SupportsTranscriptionAsync();

            // Assert
            Assert.True(result); // Falls back to true for OpenAI
        }

        [Fact]
        public async Task GetSupportedFormatsAsync_ReturnsWhisperFormats()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.GetSupportedFormatsAsync("gpt-4"))
                .ReturnsAsync(new List<string> { "mp3", "wav" });

            // Act
            var formats = await client.GetSupportedFormatsAsync();

            // Assert
            Assert.NotNull(formats);
            Assert.Contains("mp3", formats);
            Assert.Contains("wav", formats);
        }

        [Fact]
        public async Task GetSupportedLanguagesAsync_ReturnsWhisperLanguages()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.GetSupportedLanguagesAsync("gpt-4"))
                .ThrowsAsync(new Exception("Service error")); // Force fallback to default

            // Act
            var languages = await client.GetSupportedLanguagesAsync();

            // Assert
            Assert.NotNull(languages);
            Assert.Contains("en", languages);
            Assert.Contains("es", languages);
            Assert.Contains("fr", languages);
            Assert.Contains("de", languages);
            Assert.Contains("zh", languages);
            Assert.Contains("ja", languages);
            // And many more...
        }

        [Fact]
        public async Task SupportsTextToSpeechAsync_WithCapabilityService_UsesService()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsTextToSpeechAsync("gpt-4"))
                .ReturnsAsync(true);

            // Act
            var result = await client.SupportsTextToSpeechAsync();

            // Assert
            Assert.True(result);
            _capabilityServiceMock.Verify(x => x.SupportsTextToSpeechAsync("gpt-4"), Times.Once);
        }

        #endregion

        #region Realtime Audio Tests

        [Fact]
        public async Task SupportsRealtimeAsync_WithSupportedModel_ReturnsTrue()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsRealtimeAudioAsync("gpt-4o-realtime-preview"))
                .ReturnsAsync(true);

            // Act
            var result = await client.SupportsRealtimeAsync("gpt-4o-realtime-preview");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SupportsRealtimeAsync_WithUnsupportedModel_ReturnsFalse()
        {
            // Arrange
            var client = CreateOpenAIClient();
            _capabilityServiceMock.Setup(x => x.SupportsRealtimeAudioAsync("gpt-4"))
                .ReturnsAsync(false);

            // Act
            var result = await client.SupportsRealtimeAsync("gpt-4");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetRealtimeCapabilitiesAsync_ReturnsExpectedCapabilities()
        {
            // Arrange
            var client = CreateOpenAIClient();

            // Act
            var capabilities = await client.GetRealtimeCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.NotEmpty(capabilities.SupportedInputFormats);
            Assert.NotEmpty(capabilities.SupportedOutputFormats);
            Assert.NotEmpty(capabilities.AvailableVoices);
            Assert.NotEmpty(capabilities.SupportedLanguages);
            Assert.True(capabilities.SupportsFunctionCalling);
            Assert.True(capabilities.SupportsInterruptions);
        }

        #endregion

        #region Model Listing Tests

        [Fact]
        public async Task GetModelsAsync_ForOpenAI_ReturnsStandardModels()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var response = new ListModelsResponse
            {
                Data = new List<OpenAIModelData>
                {
                    new() { Id = "gpt-4", Object = "model", OwnedBy = "openai" },
                    new() { Id = "gpt-3.5-turbo", Object = "model", OwnedBy = "openai" }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            Assert.NotNull(models);
            Assert.Equal(2, models.Count);
            Assert.Contains(models, m => m.Id == "gpt-4");
            Assert.Contains(models, m => m.Id == "gpt-3.5-turbo");
        }

        [Fact]
        public async Task GetModelsAsync_ForAzure_ReturnsDeployments()
        {
            // Arrange
            var client = CreateAzureOpenAIClient();
            var response = new ConduitLLM.Providers.Providers.OpenAI.AzureOpenAIModels.ListDeploymentsResponse
            {
                Data = new List<ConduitLLM.Providers.Providers.OpenAI.AzureOpenAIModels.DeploymentInfo>
                {
                    new() { DeploymentId = "my-gpt4", Model = "gpt-4", Status = "succeeded" },
                    new() { DeploymentId = "my-gpt35", Model = "gpt-3.5-turbo", Status = "succeeded" }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            Assert.NotNull(models);
            Assert.Equal(2, models.Count);
            Assert.Contains(models, m => m.Id == "my-gpt4");
            Assert.Contains(models, m => m.Id == "my-gpt35");
        }

        [Fact]
        public async Task GetModelsAsync_WithUnauthorizedResponse_ThrowsException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var errorResponse = new
            {
                error = new
                {
                    message = "Invalid API key provided",
                    type = "invalid_request_error",
                    code = "invalid_api_key"
                }
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.GetModelsAsync());

            Assert.Contains("Invalid API key", exception.Message);
        }

        [Fact]
        public async Task GetModelsAsync_WithForbiddenResponse_ThrowsException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var errorResponse = new
            {
                error = new
                {
                    message = "Access denied. Your API key does not have permission to access this resource.",
                    type = "permission_error",
                    code = "insufficient_quota"
                }
            };

            SetupHttpResponse(HttpStatusCode.Forbidden, errorResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.GetModelsAsync());

            Assert.Contains("Access denied", exception.Message);
        }

        [Fact]
        public async Task GetModelsAsync_WithInvalidApiKey_DoesNotReturnFallbackModels()
        {
            // Arrange
            var client = CreateOpenAIClient(); 
            var errorResponse = new
            {
                error = new
                {
                    message = "Invalid API key provided: badkey",
                    type = "invalid_request_error", 
                    code = "invalid_api_key"
                }
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.GetModelsAsync());

            // Verify that no fallback models are returned - should throw exception instead
            Assert.NotNull(exception);
            Assert.Contains("Invalid API key", exception.Message);
        }

        #endregion

        #region Provider Capabilities Tests

        [Fact]
        public async Task GetCapabilitiesAsync_ForChatModel_ReturnsCorrectCapabilities()
        {
            // Arrange
            var client = CreateOpenAIClient("gpt-4");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.Equal("OpenAI", capabilities.Provider);
            Assert.Equal("gpt-4", capabilities.ModelId);
            
            // Chat parameters
            Assert.True(capabilities.ChatParameters.Temperature);
            Assert.True(capabilities.ChatParameters.MaxTokens);
            Assert.True(capabilities.ChatParameters.TopP);
            Assert.False(capabilities.ChatParameters.TopK); // OpenAI doesn't support top-k
            Assert.True(capabilities.ChatParameters.Stop);
            Assert.True(capabilities.ChatParameters.Tools);
            
            // Features
            Assert.True(capabilities.Features.Streaming);
            Assert.False(capabilities.Features.Embeddings);
            Assert.False(capabilities.Features.ImageGeneration);
            Assert.True(capabilities.Features.FunctionCalling);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForVisionModel_EnablesVisionInput()
        {
            // Arrange
            var client = CreateOpenAIClient("gpt-4o");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.True(capabilities.Features.VisionInput);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForDalleModel_EnablesImageGeneration()
        {
            // Arrange
            var client = CreateOpenAIClient("dall-e-3");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.True(capabilities.Features.ImageGeneration);
            Assert.False(capabilities.Features.Streaming);
            Assert.False(capabilities.ChatParameters.Tools);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForEmbeddingModel_EnablesEmbeddings()
        {
            // Arrange
            var client = CreateOpenAIClient("text-embedding-ada-002");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.True(capabilities.Features.Embeddings);
            Assert.False(capabilities.Features.Streaming);
            Assert.False(capabilities.ChatParameters.Tools);
        }

        #endregion

        #region Helper Methods

        private OpenAIClient CreateOpenAIClient(string modelId = "gpt-4")
        {
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var logger = CreateLogger<OpenAIClient>();

            return new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);
        }

        private OpenAIClient CreateAzureOpenAIClient(string deploymentId = "my-deployment")
        {
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.AzureOpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key",
                BaseUrl = "https://myinstance.openai.azure.com"
            };
            
            var logger = CreateLogger<OpenAIClient>();

            return new OpenAIClient(
                provider,
                keyCredential,
                deploymentId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object,
                providerName: "azure");
        }

        private void SetupHttpResponse<T>(HttpStatusCode statusCode, T content, string contentType = "application/json")
        {
            HttpContent httpContent;
            if (content is byte[] bytes)
            {
                httpContent = new ByteArrayContent(bytes);
                httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            }
            else if (content is string str)
            {
                httpContent = new StringContent(str, Encoding.UTF8, contentType);
            }
            else
            {
                var json = JsonSerializer.Serialize(content);
                httpContent = new StringContent(json, Encoding.UTF8, contentType);
            }

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = httpContent
                });
        }

        #endregion
    }
}