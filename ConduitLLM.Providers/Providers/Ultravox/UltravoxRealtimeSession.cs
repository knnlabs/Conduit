using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.Common.Models;

namespace ConduitLLM.Providers.Ultravox
{
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
            var bufferArray = new byte[4096];
            var buffer = new ArraySegment<byte>(bufferArray);

            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                ProviderRealtimeMessage? messageToYield = null;
                bool shouldBreak = false;

                try
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = System.Text.Encoding.UTF8.GetString(bufferArray, buffer.Offset, result.Count);

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