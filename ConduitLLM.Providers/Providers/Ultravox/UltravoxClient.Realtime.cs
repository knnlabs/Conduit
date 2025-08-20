using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Ultravox
{
    /// <summary>
    /// Real-time audio methods for UltravoxClient
    /// </summary>
    public partial class UltravoxClient
    {
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
            var effectiveApiKey = apiKey ?? PrimaryKeyCredential.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for Ultravox");
            }

            try
            {
                // Create WebSocket connection
                var wsBaseUrl = Provider.BaseUrl?.Replace("https://", "wss://").Replace("http://", "ws://") ?? DEFAULT_WS_BASE_URL;
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
}