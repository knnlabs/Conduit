using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.ElevenLabs
{
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
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
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