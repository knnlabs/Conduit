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
    /// Client implementation for Ultravox real-time voice AI.
    /// </summary>
    /// <remarks>
    /// Ultravox provides low-latency voice AI capabilities optimized for
    /// conversational applications including telephone systems.
    /// </remarks>
    public class UltravoxClient : BaseLLMClient, ILLMClient, IRealtimeAudioClient
    {
        private const string DEFAULT_BASE_URL = "https://api.ultravox.ai/v1";
        private const string DEFAULT_WS_BASE_URL = "wss://api.ultravox.ai/v1";
        private readonly IRealtimeMessageTranslator _translator;

        /// <summary>
        /// Initializes a new instance of the <see cref="UltravoxClient"/> class.
        /// </summary>
        public UltravoxClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<UltravoxClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(credentials, providerModelId, logger, httpClientFactory, "Ultravox", defaultModels)
        {
            var translatorLogger = logger as ILogger<UltravoxRealtimeTranslator>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<UltravoxRealtimeTranslator>();
            _translator = new UltravoxRealtimeTranslator(translatorLogger);
        }

        /// <summary>
        /// Sends a chat completion request to Ultravox.
        /// </summary>
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Ultravox is primarily a real-time voice AI provider
            // For text chat, we can use their REST API if available
            return Task.FromException<ChatCompletionResponse>(
                new NotSupportedException("Ultravox does not support text-based chat completion. Use real-time audio instead."));
        }

        /// <summary>
        /// Streams chat completion responses from Ultravox.
        /// </summary>
        public override IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Ultravox does not support streaming text chat. Use real-time audio instead.");
        }

        /// <summary>
        /// Gets the capabilities of the Ultravox real-time audio system.
        /// </summary>
        public Task<RealtimeCapabilities> GetRealtimeCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            var capabilities = new RealtimeCapabilities
            {
                SupportedInputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_8kHz,
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.G711_ULAW,
                    RealtimeAudioFormat.G711_ALAW
                },
                SupportedOutputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.G711_ULAW
                },
                MaxSessionDurationSeconds = 86400, // 24 hours
                SupportsFunctionCalling = true,
                SupportsInterruptions = true,
                TurnDetection = new TurnDetectionCapabilities
                {
                    SupportedTypes = new List<TurnDetectionType> { TurnDetectionType.ServerVAD },
                    MinSilenceThresholdMs = 20,
                    MaxSilenceThresholdMs = 200,
                    SupportsCustomParameters = true
                }
            };

            return Task.FromResult(capabilities);
        }

        /// <summary>
        /// Creates a new real-time session with Ultravox.
        /// </summary>
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveApiKey = apiKey ?? Credentials.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for Ultravox");
            }

            try
            {
                // Create WebSocket connection
                var wsBaseUrl = Credentials.BaseUrl?.Replace("https://", "wss://").Replace("http://", "ws://") ?? DEFAULT_WS_BASE_URL;
                var wsUri = new Uri($"{wsBaseUrl}/realtime?model={Uri.EscapeDataString(config.Model ?? ProviderModelId)}");
                var clientWebSocket = new ClientWebSocket();
                clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {effectiveApiKey}");
                clientWebSocket.Options.SetRequestHeader("User-Agent", "ConduitLLM/1.0");

                await clientWebSocket.ConnectAsync(wsUri, cancellationToken);

                var session = new UltravoxRealtimeSession(
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
                Logger.LogError(ex, "Failed to create Ultravox real-time session");
                throw new LLMCommunicationException("Failed to establish connection with Ultravox", ex);
            }
        }

        /// <summary>
        /// Streams audio bidirectionally with Ultravox.
        /// </summary>
        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is not UltravoxRealtimeSession ultravoxSession)
            {
                throw new ArgumentException("Session must be created by UltravoxClient", nameof(session));
            }

            return ultravoxSession.CreateDuplexStream();
        }

        /// <summary>
        /// Verifies Ultravox authentication by calling the accounts/me endpoint.
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
                
                // Update base URL to the API endpoint
                client.BaseAddress = new Uri("https://api.ultravox.ai/api/");
                
                // Use the accounts/me endpoint which is free and validates the API key
                var request = new HttpRequestMessage(HttpMethod.Get, "accounts/me");
                request.Headers.Remove("Authorization");
                request.Headers.Add("X-API-Key", effectiveApiKey);
                
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
                    $"Ultravox authentication failed: {response.StatusCode}",
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
                Logger.LogError(ex, "Unexpected error during Ultravox authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets available models from Ultravox.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Ultravox models are typically accessed via their real-time API
            // Return a static list of known models
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                new ExtendedModelInfo
                {
                    Id = "ultravox-v1",
                    OwnedBy = "ultravox",
                    Provider = "Ultravox",
                    Capabilities = new InternalModels.ModelCapabilities
                    {
                        RealtimeAudio = true,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.Realtime }
                    }
                },
                new ExtendedModelInfo
                {
                    Id = "ultravox-telephony",
                    OwnedBy = "ultravox",
                    Provider = "Ultravox",
                    Capabilities = new InternalModels.ModelCapabilities
                    {
                        RealtimeAudio = true,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.Realtime }
                    }
                }
            });
        }

        /// <summary>
        /// Creates an image from Ultravox.
        /// </summary>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("Ultravox does not support image generation. Use real-time audio instead."));
        }

        /// <summary>
        /// Creates embeddings from Ultravox.
        /// </summary>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<EmbeddingResponse>(
                new NotSupportedException("Ultravox does not support text embeddings. Use real-time audio instead."));
        }

        /// <summary>
        /// Updates the configuration of an active real-time session.
        /// </summary>
        public Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            if (session is not UltravoxRealtimeSession ultravoxSession)
            {
                return Task.FromException(
                    new ArgumentException("Session must be created by UltravoxClient", nameof(session)));
            }

            // Ultravox may support some session updates
            // For now, we'll throw not supported
            return Task.FromException(
                new NotSupportedException("Ultravox does not currently support session updates."));
        }

        /// <summary>
        /// Closes a real-time session.
        /// </summary>
        public async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is not UltravoxRealtimeSession ultravoxSession)
            {
                throw new ArgumentException("Session must be created by UltravoxClient", nameof(session));
            }

            await ultravoxSession.CloseAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if the client supports real-time audio conversations.
        /// </summary>
        public async Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Ultravox specializes in real-time audio
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Gets the capabilities of the real-time audio system.
        /// </summary>
        public Task<RealtimeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            var capabilities = new RealtimeCapabilities
            {
                SupportedInputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_8kHz,
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.G711_ULAW,
                    RealtimeAudioFormat.G711_ALAW
                },
                SupportedOutputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.G711_ULAW
                },
                SupportsInterruptions = true,
                SupportsFunctionCalling = false,
                MaxSessionDurationSeconds = 3600, // 1 hour
                SupportedLanguages = new List<string> { "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko" }
            };

            return Task.FromResult(capabilities);
        }
    }

    /// <summary>
    /// Ultravox-specific implementation of a real-time session.
    /// </summary>
    internal class UltravoxRealtimeSession : RealtimeSession
    {
        private readonly IRealtimeMessageTranslator _translator;
        private readonly ILogger _logger;
        private readonly RealtimeSessionConfig _config;
        private readonly ClientWebSocket _webSocket;

        public UltravoxRealtimeSession(
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
            Provider = "Ultravox";
        }

        /// <summary>
        /// Configures the Ultravox session with initial parameters.
        /// </summary>
        public async Task ConfigureAsync(RealtimeSessionConfig config, CancellationToken cancellationToken)
        {
            var configMessage = new ProviderRealtimeMessage
            {
                Type = "session.configure",
                Data = new Dictionary<string, object>
                {
                    ["voice"] = config.Voice ?? "default",
                    ["language"] = config.Language ?? "en-US",
                    ["input_format"] = config.InputFormat.ToString().ToLower(),
                    ["output_format"] = config.OutputFormat.ToString().ToLower(),
                    ["vad_enabled"] = config.TurnDetection?.Type == TurnDetectionType.ServerVAD,
                    ["interruption_enabled"] = config.TurnDetection?.Enabled ?? true,
                    ["system_prompt"] = config.SystemPrompt ?? "You are a helpful AI assistant."
                }
            };

            await SendMessageAsync(configMessage, cancellationToken);
        }

        /// <summary>
        /// Sends a message through the Ultravox session.
        /// </summary>
        public async Task SendMessageAsync(ProviderRealtimeMessage message, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not open");

            // Convert to a concrete RealtimeMessage type for translator
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
        /// Receives messages from the Ultravox session.
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
                    _logger.LogError(ex, "Error receiving message from Ultravox");
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
        /// Duplex stream implementation for Ultravox.
        /// </summary>
        private class RealtimeDuplexStream : IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>
        {
            private readonly UltravoxRealtimeSession _session;

            public RealtimeDuplexStream(UltravoxRealtimeSession session)
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
                    var response = new RealtimeResponse
                    {
                        SessionId = message.SessionId,
                        Timestamp = message.Timestamp,
                        EventType = RealtimeEventType.AudioDelta
                    };

                    // Map Ultravox message types to RealtimeResponse
                    switch (message.Type?.ToLower())
                    {
                        case "audio":
                            response.Audio = new AudioDelta
                            {
                                Data = message.Data?.ContainsKey("audio") == true
                                    ? Convert.FromBase64String(message.Data["audio"].ToString()!)
                                    : Array.Empty<byte>(),
                                IsComplete = false
                            };
                            response.EventType = RealtimeEventType.AudioDelta;
                            break;

                        case "transcript":
                            response.Transcription = new TranscriptionDelta
                            {
                                Text = message.Data?.ContainsKey("text") == true
                                    ? message.Data["text"].ToString() ?? string.Empty
                                    : string.Empty,
                                IsFinal = message.Data?.ContainsKey("is_final") == true
                                    && bool.Parse(message.Data["is_final"].ToString()!),
                                Role = "assistant"
                            };
                            response.EventType = RealtimeEventType.TranscriptionDelta;
                            break;

                        case "error":
                            response.Error = new ErrorInfo
                            {
                                Code = message.Data?.ContainsKey("code") == true
                                    ? message.Data["code"].ToString() ?? "UNKNOWN"
                                    : "UNKNOWN",
                                Message = message.Data?.ContainsKey("message") == true
                                    ? message.Data["message"].ToString() ?? "Unknown error"
                                    : "Unknown error"
                            };
                            response.EventType = RealtimeEventType.Error;
                            break;
                    }

                    yield return response;
                }
            }

            public async ValueTask CompleteAsync()
            {
                await _session.CloseAsync(CancellationToken.None);
            }

            public void Dispose()
            {
                // Cleanup handled by session
            }
        }
    }
}
