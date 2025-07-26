using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client implementation for ElevenLabs voice AI services.
    /// </summary>
    /// <remarks>
    /// ElevenLabs provides high-quality text-to-speech and conversational AI
    /// with support for voice cloning and real-time voice synthesis.
    /// </remarks>
    public class ElevenLabsClient : BaseLLMClient, ILLMClient, ITextToSpeechClient, IRealtimeAudioClient
    {
        private const string DEFAULT_BASE_URL = "https://api.elevenlabs.io/v1";
        private const string WS_BASE_URL = "wss://api.elevenlabs.io/v1";
        private readonly IRealtimeMessageTranslator _translator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElevenLabsClient"/> class.
        /// </summary>
        public ElevenLabsClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<ElevenLabsClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(credentials, providerModelId, logger, httpClientFactory, "ElevenLabs", defaultModels)
        {
            var translatorLogger = logger as ILogger<ElevenLabsRealtimeTranslator>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<ElevenLabsRealtimeTranslator>();
            _translator = new ElevenLabsRealtimeTranslator(translatorLogger);
        }

        /// <summary>
        /// Sends a chat completion request to ElevenLabs.
        /// </summary>
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // ElevenLabs is primarily a voice AI provider
            return Task.FromException<ChatCompletionResponse>(
                new NotSupportedException("ElevenLabs does not support text-based chat completion. Use text-to-speech or real-time audio instead."));
        }

        /// <summary>
        /// Streams chat completion responses from ElevenLabs.
        /// </summary>
        public override IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ElevenLabs does not support streaming text chat. Use text-to-speech or real-time audio instead.");
        }

        /// <summary>
        /// Creates speech audio from text using ElevenLabs.
        /// </summary>
        public async Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateSpeech");

            var effectiveApiKey = apiKey ?? Credentials.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            using var httpClient = CreateHttpClient(effectiveApiKey);

            // ElevenLabs uses voice IDs instead of voice names
            var voiceId = request.Voice ?? "21m00Tcm4TlvDq8ikWAM"; // Default voice ID
            var model = request.Model ?? GetDefaultTextToSpeechModel();

            var baseUrl = Credentials.BaseUrl ?? DEFAULT_BASE_URL;
            var requestUrl = $"{baseUrl}/text-to-speech/{voiceId}";

            var requestBody = new Dictionary<string, object>
            {
                ["text"] = request.Input,
                ["model_id"] = model,
                ["voice_settings"] = new Dictionary<string, object>
                {
                    ["stability"] = request.VoiceSettings?.Stability ?? 0.5,
                    ["similarity_boost"] = request.VoiceSettings?.SimilarityBoost ?? 0.5,
                    ["style"] = request.VoiceSettings?.Style ?? "default"
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, DefaultJsonOptions);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
            }

            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return new TextToSpeechResponse
            {
                AudioData = audioData,
                Format = request.ResponseFormat?.ToString().ToLower() ?? "mp3",
                SampleRate = 22050, // ElevenLabs default
                Duration = null // Would need to calculate from audio data
            };
        }

        /// <summary>
        /// Streams speech audio from text using ElevenLabs.
        /// </summary>
        public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamSpeech");

            var effectiveApiKey = apiKey ?? Credentials.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            using var httpClient = CreateHttpClient(effectiveApiKey);

            var voiceId = request.Voice ?? "21m00Tcm4TlvDq8ikWAM";
            var model = request.Model ?? GetDefaultTextToSpeechModel();

            var baseUrl = Credentials.BaseUrl ?? DEFAULT_BASE_URL;
            var requestUrl = $"{baseUrl}/text-to-speech/{voiceId}/stream";

            var requestBody = new Dictionary<string, object>
            {
                ["text"] = request.Input,
                ["model_id"] = model,
                ["voice_settings"] = new Dictionary<string, object>
                {
                    ["stability"] = request.VoiceSettings?.Stability ?? 0.5,
                    ["similarity_boost"] = request.VoiceSettings?.SimilarityBoost ?? 0.5
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, DefaultJsonOptions);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                var chunk = new byte[bytesRead];
                Array.Copy(buffer, 0, chunk, 0, bytesRead);

                yield return new AudioChunk
                {
                    Data = chunk,
                    IsFinal = false
                };
            }

            // Final chunk
            yield return new AudioChunk
            {
                Data = Array.Empty<byte>(),
                IsFinal = true
            };
        }

        /// <summary>
        /// Lists available voices from ElevenLabs.
        /// </summary>
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveApiKey = apiKey ?? Credentials.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            using var httpClient = CreateHttpClient(effectiveApiKey);

            var baseUrl = Credentials.BaseUrl ?? DEFAULT_BASE_URL;
            var response = await httpClient.GetAsync($"{baseUrl}/voices", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var voicesResponse = JsonSerializer.Deserialize<ElevenLabsVoicesResponse>(jsonContent, DefaultJsonOptions);

            return voicesResponse?.Voices?.Select(v => new VoiceInfo
            {
                VoiceId = v.VoiceId,
                Name = v.Name,
                SupportedLanguages = new List<string> { v.Labels?.Language ?? "en" },
                Gender = v.Labels?.Gender?.ToLower() switch
                {
                    "male" => VoiceGender.Male,
                    "female" => VoiceGender.Female,
                    _ => VoiceGender.Neutral
                },
                SampleUrl = v.PreviewUrl,
                Metadata = new Dictionary<string, object> { { "provider", "ElevenLabs" } }
            }).ToList() ?? new List<VoiceInfo>();
        }

        /// <summary>
        /// Gets the audio formats supported by ElevenLabs.
        /// </summary>
        public async Task<List<string>> GetSupportedFormatsAsync(
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new List<string>
            {
                "mp3",
                "wav",
                "pcm",
                "ogg",
                "flac"
            });
        }

        /// <summary>
        /// Checks if the client supports text-to-speech synthesis.
        /// </summary>
        public async Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Updates the configuration of an active real-time session.
        /// </summary>
        public async Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            if (session is not ElevenLabsRealtimeSession elevenLabsSession)
            {
                throw new ArgumentException("Session must be created by ElevenLabsClient", nameof(session));
            }

            // Send update message to ElevenLabs
            var updateMessage = new ProviderRealtimeMessage
            {
                Type = "update_session",
                Data = new Dictionary<string, object>
                {
                    ["voice_id"] = session.Config.Voice ?? "rachel",
                    ["language"] = "en",
                    ["system_prompt"] = updates.SystemPrompt ?? session.Config.SystemPrompt ?? string.Empty
                }
            };

            await elevenLabsSession.SendMessageAsync(updateMessage, cancellationToken);
        }

        /// <summary>
        /// Closes an active real-time session.
        /// </summary>
        public async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is not ElevenLabsRealtimeSession elevenLabsSession)
            {
                throw new ArgumentException("Session must be created by ElevenLabsClient", nameof(session));
            }

            await elevenLabsSession.CloseAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if the client supports real-time audio conversations.
        /// </summary>
        public async Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // ElevenLabs supports real-time audio with conversational AI models
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Gets the capabilities of the ElevenLabs real-time audio system.
        /// </summary>
        public Task<RealtimeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            var capabilities = new RealtimeCapabilities
            {
                SupportedInputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.PCM16_24kHz,
                    RealtimeAudioFormat.PCM16_48kHz
                },
                SupportedOutputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_24kHz,
                    RealtimeAudioFormat.PCM16_48kHz
                },
                MaxSessionDurationSeconds = 3600, // 1 hour
                SupportsFunctionCalling = false,
                SupportsInterruptions = true,
                TurnDetection = new TurnDetectionCapabilities
                {
                    SupportedTypes = new List<TurnDetectionType> { TurnDetectionType.ServerVAD },
                    MinSilenceThresholdMs = 50,
                    MaxSilenceThresholdMs = 500,
                    SupportsCustomParameters = true
                }
            };

            return Task.FromResult(capabilities);
        }

        /// <summary>
        /// Creates a new real-time session with ElevenLabs Conversational AI.
        /// </summary>
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveApiKey = apiKey ?? Credentials.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            try
            {
                // Create WebSocket connection to ElevenLabs Conversational AI
                var wsUri = new Uri($"{WS_BASE_URL}/conversational/websocket");
                var clientWebSocket = new ClientWebSocket();
                clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {effectiveApiKey}");
                clientWebSocket.Options.SetRequestHeader("User-Agent", "ConduitLLM/1.0");

                await clientWebSocket.ConnectAsync(wsUri, cancellationToken);

                // Ensure model is set to default if not provided
                if (string.IsNullOrEmpty(config.Model))
                {
                    config.Model = GetDefaultRealtimeModel();
                }

                var session = new ElevenLabsRealtimeSession(
                    clientWebSocket,
                    _translator,
                    Logger,
                    config);

                // Send initial configuration
                await session.ConfigureAsync(config, cancellationToken);

                return session;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create ElevenLabs real-time session");
                throw new LLMCommunicationException("Failed to establish connection with ElevenLabs", ex);
            }
        }

        /// <summary>
        /// Streams audio bidirectionally with ElevenLabs.
        /// </summary>
        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is not ElevenLabsRealtimeSession elevenLabsSession)
            {
                throw new ArgumentException("Session must be created by ElevenLabsClient", nameof(session));
            }

            return elevenLabsSession.CreateDuplexStream();
        }

        /// <summary>
        /// Verifies ElevenLabs authentication by calling the user endpoint.
        /// This is a free API call that validates the API key.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure("API key is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                
                // Use the user endpoint which is free and validates the API key
                var request = new HttpRequestMessage(HttpMethod.Get, "user");
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success($"Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid API key");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Access denied. Check your API key permissions");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"ElevenLabs authentication failed: {response.StatusCode}",
                    errorContent);
            }
            catch (HttpRequestException ex)
            {
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Network error during authentication: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException)
            {
                return Core.Interfaces.AuthenticationResult.Failure("Authentication request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during ElevenLabs authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets available models from ElevenLabs.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                new ExtendedModelInfo
                {
                    Id = "eleven_monolingual_v1",
                    OwnedBy = "elevenlabs",
                    Provider = "ElevenLabs",
                    Capabilities = new InternalModels.ModelCapabilities
                    {
                        Chat = false,
                        TextToSpeech = true,
                        RealtimeAudio = false,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.TextToSpeech }
                    }
                },
                new ExtendedModelInfo
                {
                    Id = "eleven_multilingual_v2",
                    OwnedBy = "elevenlabs",
                    Provider = "ElevenLabs",
                    Capabilities = new InternalModels.ModelCapabilities
                    {
                        Chat = false,
                        TextToSpeech = true,
                        RealtimeAudio = false,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.TextToSpeech }
                    }
                },
                new ExtendedModelInfo
                {
                    Id = "eleven_conversational_v1",
                    OwnedBy = "elevenlabs",
                    Provider = "ElevenLabs",
                    Capabilities = new InternalModels.ModelCapabilities
                    {
                        Chat = false,
                        TextToSpeech = false,
                        RealtimeAudio = true,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.Realtime }
                    }
                }
            });
        }

        /// <summary>
        /// Creates image generation from ElevenLabs.
        /// </summary>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("ElevenLabs does not support image generation. Use text-to-speech or real-time audio instead."));
        }

        /// <summary>
        /// Creates embeddings from ElevenLabs.
        /// </summary>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<EmbeddingResponse>(
                new NotSupportedException("ElevenLabs does not support text embeddings. Use text-to-speech or real-time audio instead."));
        }


        // Helper classes for ElevenLabs API responses
        private class ElevenLabsVoicesResponse
        {
            public List<ElevenLabsVoice>? Voices { get; set; }
        }

        private class ElevenLabsVoice
        {
            public string VoiceId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? PreviewUrl { get; set; }
            public ElevenLabsVoiceLabels? Labels { get; set; }
        }

        private class ElevenLabsVoiceLabels
        {
            public string? Language { get; set; }
            public string? Gender { get; set; }
        }

        #region Configuration Helpers

        /// <summary>
        /// Gets the default text-to-speech model from configuration or falls back to eleven_monolingual_v1.
        /// </summary>
        private string GetDefaultTextToSpeechModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Audio?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant())?.TextToSpeechModel;

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Audio?.DefaultTextToSpeechModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Fallback to hardcoded default for backward compatibility
            return "eleven_monolingual_v1";
        }

        /// <summary>
        /// Gets the default realtime model from configuration or falls back to eleven_conversational_v1.
        /// </summary>
        private string GetDefaultRealtimeModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Realtime?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Realtime?.DefaultRealtimeModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Fallback to hardcoded default for backward compatibility
            return "eleven_conversational_v1";
        }

        #endregion
    }

    /// <summary>
    /// ElevenLabs-specific implementation of a real-time session.
    /// </summary>
    internal class ElevenLabsRealtimeSession : RealtimeSession
    {
        private readonly IRealtimeMessageTranslator _translator;
        private readonly ILogger _logger;
        private readonly RealtimeSessionConfig _config;
        private readonly ClientWebSocket _webSocket;

        public ElevenLabsRealtimeSession(
            ClientWebSocket webSocket,
            IRealtimeMessageTranslator translator,
            ILogger logger,
            RealtimeSessionConfig config)
            : base()
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Config = config;
            Provider = "ElevenLabs";
        }

        /// <summary>
        /// Configures the ElevenLabs session with initial parameters.
        /// </summary>
        public async Task ConfigureAsync(RealtimeSessionConfig config, CancellationToken cancellationToken)
        {
            var configMessage = new ProviderRealtimeMessage
            {
                Type = "configure",
                Data = new Dictionary<string, object>
                {
                    ["voice_id"] = config.Voice ?? "21m00Tcm4TlvDq8ikWAM",
                    ["language"] = config.Language ?? "en",
                    ["model_id"] = config.Model ?? "eleven_conversational_v1", // Model should be set in CreateSessionAsync
                    ["voice_settings"] = new Dictionary<string, object>
                    {
                        ["stability"] = 0.5,
                        ["similarity_boost"] = 0.8
                    },
                    ["generation_config"] = new Dictionary<string, object>
                    {
                        ["chunk_size"] = 200, // ms
                        ["streaming"] = true
                    }
                }
            };

            await SendMessageAsync(configMessage, cancellationToken);
        }

        /// <summary>
        /// Sends a message through the ElevenLabs session.
        /// </summary>
        public async Task SendMessageAsync(ProviderRealtimeMessage message, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not open");

            // Convert ProviderRealtimeMessage to RealtimeMessage for translator
            var realtimeMessage = new RealtimeAudioFrame
            {
                SessionId = message.SessionId,
                Timestamp = message.Timestamp
            };

            var jsonMessage = await _translator.TranslateToProviderAsync(realtimeMessage);
            var buffer = System.Text.Encoding.UTF8.GetBytes(jsonMessage);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
        }

        /// <summary>
        /// Receives messages from the ElevenLabs session.
        /// </summary>
        public async IAsyncEnumerable<ProviderRealtimeMessage> ReceiveMessagesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);

            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                ProviderRealtimeMessage? messageToYield = null;
                bool shouldBreak = false;

                try
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = System.Text.Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, result.Count);

                        // Parse the message
                        messageToYield = new ProviderRealtimeMessage
                        {
                            Type = "message",
                            Data = new Dictionary<string, object> { ["raw"] = json },
                            Timestamp = DateTime.UtcNow
                        };
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        shouldBreak = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving message from ElevenLabs");
                    messageToYield = new ProviderRealtimeMessage
                    {
                        Type = "error",
                        Data = new Dictionary<string, object>
                        {
                            ["error"] = "Receive error",
                            ["details"] = ex.Message
                        },
                        Timestamp = DateTime.UtcNow
                    };
                    shouldBreak = true;
                }

                if (messageToYield != null)
                {
                    yield return messageToYield;
                }

                if (shouldBreak)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Creates a duplex stream for bidirectional communication.
        /// </summary>
        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> CreateDuplexStream()
        {
            return new RealtimeDuplexStream(this);
        }

        /// <summary>
        /// Closes the real-time session.
        /// </summary>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session closed", cancellationToken);
            }
            State = SessionState.Closed;
        }

        /// <summary>
        /// Duplex stream implementation for ElevenLabs.
        /// </summary>
        private class RealtimeDuplexStream : IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>
        {
            private readonly ElevenLabsRealtimeSession _session;

            public RealtimeDuplexStream(ElevenLabsRealtimeSession session)
            {
                _session = session ?? throw new ArgumentNullException(nameof(session));
            }

            public bool IsConnected => _session.State == SessionState.Connected;

            public async ValueTask SendAsync(RealtimeAudioFrame item, CancellationToken cancellationToken = default)
            {
                var message = new ProviderRealtimeMessage
                {
                    Type = "audio",
                    Data = new Dictionary<string, object>
                    {
                        ["audio"] = Convert.ToBase64String(item.AudioData),
                        ["timestamp"] = item.Timestamp
                    }
                };
                await _session.SendMessageAsync(message, cancellationToken);
            }

            public async IAsyncEnumerable<RealtimeResponse> ReceiveAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var message in _session.ReceiveMessagesAsync(cancellationToken))
                {
                    // Convert ProviderRealtimeMessage to RealtimeResponse
                    var response = new RealtimeResponse
                    {
                        SessionId = message.SessionId,
                        Timestamp = message.Timestamp,
                        EventType = RealtimeEventType.AudioDelta
                    };

                    if (message.Type == "audio" && message.Data != null)
                    {
                        if (message.Data.TryGetValue("audio", out var audioBase64) && audioBase64 is string base64String)
                        {
                            response.Audio = new AudioDelta
                            {
                                Data = Convert.FromBase64String(base64String),
                                IsComplete = false
                            };
                            response.EventType = RealtimeEventType.AudioDelta;
                        }
                    }
                    else if (message.Type == "text" && message.Data != null)
                    {
                        if (message.Data.TryGetValue("text", out var text) && text is string textString)
                        {
                            response.TextResponse = textString;
                            response.EventType = RealtimeEventType.TextResponse;
                        }
                    }
                    else if (message.Type == "error" && message.Data != null)
                    {
                        if (message.Data.TryGetValue("error", out var error))
                        {
                            response.Error = new ErrorInfo
                            {
                                Code = "ELEVENLABS_ERROR",
                                Message = error?.ToString() ?? "Unknown error"
                            };
                            response.EventType = RealtimeEventType.Error;
                        }
                    }

                    yield return response;
                }
            }

            public async ValueTask CompleteAsync()
            {
                // Send end-of-stream message
                var message = new ProviderRealtimeMessage
                {
                    Type = "end_stream",
                    Timestamp = DateTime.UtcNow
                };
                await _session.SendMessageAsync(message, CancellationToken.None);
            }
        }
    }
}
