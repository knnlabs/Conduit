using System.Runtime.CompilerServices;
using System.Text.Json;

using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.Helpers;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing realtime audio functionality.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Creates a realtime audio session with OpenAI's API.
        /// </summary>
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // OpenAI Realtime API uses WebSocket connection
            var wsUrl = UrlBuilder.ToWebSocketUrl(BaseUrl);
            
            // Model must be specified
            var model = config.Model;
            if (string.IsNullOrWhiteSpace(model))
            {
                model = GetDefaultRealtimeModel();
                if (string.IsNullOrWhiteSpace(model))
                {
                    throw new ArgumentException("Model must be specified for realtime audio sessions", nameof(config));
                }
            }
            
            wsUrl = UrlBuilder.Combine(wsUrl, "realtime");
            wsUrl = UrlBuilder.AppendQueryString(wsUrl, ("model", model));

            var effectiveApiKey = apiKey ?? PrimaryKeyCredential.ApiKey ?? throw new InvalidOperationException("API key is required");
            var session = new OpenAIRealtimeSession(wsUrl, effectiveApiKey, config, Logger);
            await session.ConnectAsync(cancellationToken);

            return session;
        }

        /// <summary>
        /// Creates a realtime audio session with OpenAI's API.
        /// </summary>
        /// <remarks>
        /// This method is obsolete and will be removed in the next major version.
        /// Use CreateSessionAsync instead, which has the correct parameter order.
        /// </remarks>
        [Obsolete("Use CreateSessionAsync instead. This method will be removed in the next major version.")]
        public async Task<RealtimeSession> ConnectAsync(
            string? apiKey,
            RealtimeSessionConfig config,
            CancellationToken cancellationToken = default)
        {
            // Forward to new method with corrected parameter order
            return await CreateSessionAsync(config, apiKey, cancellationToken);
        }

        /// <summary>
        /// Checks if the specified model supports realtime audio.
        /// </summary>
        public async Task<bool> SupportsRealtimeAsync(string model, CancellationToken cancellationToken = default)
        {
            if (_capabilityService != null)
            {
                try
                {
                    return await _capabilityService.SupportsRealtimeAudioAsync(model);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to check realtime capability via ModelCapabilityService, falling back to default");
                }
            }
            
            // Fallback: Check against known OpenAI realtime models
            var supportedModels = new[] { "gpt-4o-realtime-preview", "gpt-4o-realtime-preview-2024-10-01" };
            return supportedModels.Contains(model);
        }

        /// <summary>
        /// Gets the realtime capabilities for OpenAI.
        /// </summary>
        public Task<RealtimeCapabilities> GetRealtimeCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RealtimeCapabilities
            {
                SupportedInputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_24kHz,
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.G711_ULAW,
                    RealtimeAudioFormat.G711_ALAW
                },
                SupportedOutputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_24kHz,
                    RealtimeAudioFormat.PCM16_16kHz
                },
                AvailableVoices = new List<VoiceInfo>
                {
                    new VoiceInfo { VoiceId = "alloy", Name = "Alloy", Gender = VoiceGender.Neutral },
                    new VoiceInfo { VoiceId = "echo", Name = "Echo", Gender = VoiceGender.Male },
                    new VoiceInfo { VoiceId = "shimmer", Name = "Shimmer", Gender = VoiceGender.Female }
                },
                SupportedLanguages = new List<string> { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko" },
                SupportsFunctionCalling = true,
                SupportsInterruptions = true
            });
        }

        /// <summary>
        /// Creates a stream for realtime audio communication.
        /// </summary>
        public Core.Interfaces.IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is not OpenAIRealtimeSession openAISession)
                throw new InvalidOperationException("Session must be created by this client");

            return new OpenAIRealtimeStream(openAISession, Logger as ILogger<OpenAIClient> ??
                throw new InvalidOperationException("Logger must be ILogger<OpenAIClient>"));
        }

        /// <summary>
        /// Updates an existing realtime session.
        /// </summary>
        public async Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            if (session is not OpenAIRealtimeSession openAISession)
                throw new InvalidOperationException("Session must be created by this client");

            // For OpenAI, we need to create a provider-specific message
            var providerMessage = new Dictionary<string, object>
            {
                ["type"] = "session.update",
                ["session"] = new Dictionary<string, object?>()
            };

            var sessionData = (Dictionary<string, object?>)providerMessage["session"];

            if (updates.SystemPrompt != null)
                sessionData["instructions"] = updates.SystemPrompt;

            if (updates.Temperature.HasValue)
                sessionData["temperature"] = updates.Temperature.Value;

            if (updates.VoiceSettings != null && updates.VoiceSettings.Speed.HasValue)
                sessionData["speed"] = updates.VoiceSettings.Speed.Value;

            if (updates.TurnDetection != null)
            {
                sessionData["turn_detection"] = new Dictionary<string, object>
                {
                    ["type"] = updates.TurnDetection.Type.ToString().ToLowerInvariant(),
                    ["threshold"] = updates.TurnDetection.Threshold ?? 0.5,
                    ["prefix_padding_ms"] = updates.TurnDetection.PrefixPaddingMs ?? 300,
                    ["silence_duration_ms"] = updates.TurnDetection.SilenceThresholdMs ?? 500
                };
            }

            if (updates.Tools != null)
            {
                sessionData["tools"] = updates.Tools.Select(t => new
                {
                    type = "function",
                    function = new
                    {
                        name = t.Function?.Name,
                        description = t.Function?.Description,
                        parameters = t.Function?.Parameters
                    }
                }).ToList();
            }

            // Convert to JSON and send as a raw message
            var json = JsonSerializer.Serialize(providerMessage, DefaultJsonOptions);
            await openAISession.SendRawMessageAsync(json, cancellationToken);
        }

        /// <summary>
        /// Closes a realtime session.
        /// </summary>
        public async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            session?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Checks if realtime audio is supported.
        /// </summary>
        Task<bool> Core.Interfaces.IRealtimeAudioClient.SupportsRealtimeAsync(string? apiKey, CancellationToken cancellationToken)
        {
            // OpenAI supports real-time with appropriate models
            return Task.FromResult(true);
        }

        /// <summary>
        /// Gets realtime capabilities.
        /// </summary>
        Task<RealtimeCapabilities> Core.Interfaces.IRealtimeAudioClient.GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            return GetRealtimeCapabilitiesAsync(cancellationToken);
        }

        /// <summary>
        /// OpenAI-specific realtime stream implementation.
        /// </summary>
        private class OpenAIRealtimeStream : Core.Interfaces.IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>
        {
            private readonly OpenAIRealtimeSession _session;
            private readonly ILogger<OpenAIClient> _logger;

            public OpenAIRealtimeStream(OpenAIRealtimeSession session, ILogger<OpenAIClient> logger)
            {
                _session = session;
                _logger = logger;
            }

            public bool IsConnected => _session.State == SessionState.Connected || _session.State == SessionState.Active;

            public async ValueTask SendAsync(RealtimeAudioFrame item, CancellationToken cancellationToken = default)
            {
                if (item.AudioData != null && item.AudioData.Length > 0)
                {
                    // For OpenAI, we need to send the raw provider-specific message
                    var providerMessage = new Dictionary<string, object>
                    {
                        ["type"] = "input_audio_buffer.append",
                        ["audio"] = Convert.ToBase64String(item.AudioData)
                    };

                    var json = JsonSerializer.Serialize(providerMessage, DefaultJsonOptions);
                    await _session.SendRawMessageAsync(json, cancellationToken);
                }
            }

            public async IAsyncEnumerable<RealtimeResponse> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var message in _session.ReceiveMessagesAsync(cancellationToken))
                {
                    var response = ConvertToRealtimeResponse(message);
                    if (response != null)
                        yield return response;
                }
            }

            public async ValueTask CompleteAsync()
            {
                var providerMessage = new Dictionary<string, object>
                {
                    ["type"] = "input_audio_buffer.commit"
                };

                var json = JsonSerializer.Serialize(providerMessage, DefaultJsonOptions);
                await _session.SendRawMessageAsync(json, CancellationToken.None);
            }

            private RealtimeResponse? ConvertToRealtimeResponse(RealtimeMessage message)
            {
                // The translator should have already converted to RealtimeResponse
                if (message is RealtimeResponse response)
                    return response;

                // If not, we have an unexpected message type
                _logger.LogWarning("Received unexpected message type: {Type}", message.GetType().Name);
                return null;
            }
        }
    }
}