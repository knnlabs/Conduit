using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.ElevenLabs.Services
{
    /// <summary>
    /// Service for handling ElevenLabs real-time audio operations.
    /// </summary>
    internal class ElevenLabsRealtimeService
    {
        private const string WS_BASE_URL = "wss://api.elevenlabs.io/v1";
        private readonly IRealtimeMessageTranslator _translator;
        private readonly ILogger _logger;

        public ElevenLabsRealtimeService(IRealtimeMessageTranslator translator, ILogger logger)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new real-time session with ElevenLabs Conversational AI.
        /// </summary>
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string apiKey,
            string defaultModel,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            try
            {
                // Create WebSocket connection to ElevenLabs Conversational AI
                var wsUri = new Uri($"{WS_BASE_URL}/conversational/websocket");
                var clientWebSocket = new ClientWebSocket();
                clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                clientWebSocket.Options.SetRequestHeader("User-Agent", "ConduitLLM/1.0");

                await clientWebSocket.ConnectAsync(wsUri, cancellationToken);

                // Ensure model is set to default if not provided
                if (string.IsNullOrEmpty(config.Model))
                {
                    config.Model = defaultModel;
                }

                var session = new ElevenLabsRealtimeSession(
                    clientWebSocket,
                    _translator,
                    _logger,
                    config);

                // Send initial configuration
                await session.ConfigureAsync(config, cancellationToken);

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create ElevenLabs real-time session");
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
        public Task<bool> SupportsRealtimeAsync(CancellationToken cancellationToken = default)
        {
            // ElevenLabs supports real-time audio with conversational AI models
            return Task.FromResult(true);
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
    }
}