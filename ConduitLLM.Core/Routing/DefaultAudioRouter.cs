using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Default implementation of the audio router that routes requests to appropriate providers.
    /// </summary>
    public class DefaultAudioRouter : IAudioRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<DefaultAudioRouter> _logger;
        private readonly IModelCapabilityDetector _capabilityDetector;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAudioRouter"/> class.
        /// </summary>
        public DefaultAudioRouter(
            ILLMClientFactory clientFactory,
            ILogger<DefaultAudioRouter> logger,
            IModelCapabilityDetector capabilityDetector)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        }

        /// <summary>
        /// Gets an audio transcription client based on the request requirements.
        /// </summary>
        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // First try to get client for the specific model if provided
                if (!string.IsNullOrEmpty(request.Model))
                {
                    try
                    {
                        var client = _clientFactory.GetClient(request.Model);

                        // Check if the client supports audio transcription
                        if (client is IAudioTranscriptionClient audioClient)
                        {
_logger.LogDebug("Found audio transcription client for model {Model}", request.Model.Replace(Environment.NewLine, ""));
                            return audioClient;
                        }
                        else
                        {
_logger.LogWarning("Client for model {Model} does not support audio transcription", request.Model.Replace(Environment.NewLine, ""));
                        }
                    }
                    catch (Exception ex)
                    {
_logger.LogWarning(ex, "Failed to get client for model {Model}".Replace(Environment.NewLine, ""), request.Model.Replace(Environment.NewLine, ""));
                    }
                }

                // If no specific model or failed, try to find any available transcription provider
                var transcriptionProviders = await GetTranscriptionProvidersAsync();

                foreach (var provider in transcriptionProviders)
                {
                    try
                    {
                        var client = _clientFactory.GetClientByProvider(provider);
                        if (client is IAudioTranscriptionClient audioClient)
                        {
                            _logger.LogInformation("Using {Provider} for audio transcription", provider);
                            return audioClient;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to initialize {Provider} for transcription", provider);
                    }
                }

                _logger.LogError("No audio transcription providers available");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing audio transcription request");
                return null;
            }
        }

        /// <summary>
        /// Gets a text-to-speech client based on the request requirements.
        /// </summary>
        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            TextToSpeechRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // First try to get client for the specific model if provided
                if (!string.IsNullOrEmpty(request.Model))
                {
                    try
                    {
                        var client = _clientFactory.GetClient(request.Model);

                        // Check if the client supports text-to-speech
                        if (client is ITextToSpeechClient ttsClient)
                        {
_logger.LogDebug("Found text-to-speech client for model {Model}", request.Model.Replace(Environment.NewLine, ""));
                            return ttsClient;
                        }
                        else
                        {
_logger.LogWarning("Client for model {Model} does not support text-to-speech", request.Model.Replace(Environment.NewLine, ""));
                        }
                    }
                    catch (Exception ex)
                    {
_logger.LogWarning(ex, "Failed to get client for model {Model}".Replace(Environment.NewLine, ""), request.Model.Replace(Environment.NewLine, ""));
                    }
                }

                // If no specific model or failed, try to find any available TTS provider
                var ttsProviders = await GetTextToSpeechProvidersAsync();

                // Prefer providers that support the requested voice
                if (!string.IsNullOrEmpty(request.Voice))
                {
                    foreach (var provider in ttsProviders)
                    {
                        try
                        {
                            var client = _clientFactory.GetClientByProvider(provider);
                            if (client is ITextToSpeechClient ttsClient)
                            {
                                // Check if provider supports the requested voice
                                var voices = await ttsClient.ListVoicesAsync(virtualKey, cancellationToken);
                                if (voices.Any(v => v.VoiceId == request.Voice || v.Name == request.Voice))
                                {
_logger.LogInformation("Using {Provider} for TTS with voice {Voice}", provider.Replace(Environment.NewLine, ""), request.Voice.Replace(Environment.NewLine, ""));
                                    return ttsClient;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to check voices for {Provider}", provider);
                        }
                    }
                }

                // Fall back to any available TTS provider
                foreach (var provider in ttsProviders)
                {
                    try
                    {
                        var client = _clientFactory.GetClientByProvider(provider);
                        if (client is ITextToSpeechClient ttsClient)
                        {
                            _logger.LogInformation("Using {Provider} for text-to-speech", provider);
                            return ttsClient;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to initialize {Provider} for TTS", provider);
                    }
                }

                _logger.LogError("No text-to-speech providers available");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing text-to-speech request");
                return null;
            }
        }

        /// <summary>
        /// Gets a real-time audio client based on the session configuration.
        /// </summary>
        public async Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            RealtimeSessionConfig config,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // First try to get client for the specific model if provided
                if (!string.IsNullOrEmpty(config.Model))
                {
                    try
                    {
                        var client = _clientFactory.GetClient(config.Model);

                        // Check if the client supports real-time audio
                        if (client is IRealtimeAudioClient realtimeClient)
                        {
_logger.LogDebug("Found real-time audio client for model {Model}", config.Model.Replace(Environment.NewLine, ""));
                            return realtimeClient;
                        }
                        else
                        {
_logger.LogWarning("Client for model {Model} does not support real-time audio", config.Model.Replace(Environment.NewLine, ""));
                        }
                    }
                    catch (Exception ex)
                    {
_logger.LogWarning(ex, "Failed to get client for model {Model}".Replace(Environment.NewLine, ""), config.Model.Replace(Environment.NewLine, ""));
                    }
                }

                // If no specific model or failed, try to find any available real-time provider
                var realtimeProviders = await GetRealtimeProvidersAsync();

                // Check capabilities match
                foreach (var provider in realtimeProviders)
                {
                    try
                    {
                        var client = _clientFactory.GetClientByProvider(provider);
                        if (client is IRealtimeAudioClient realtimeClient)
                        {
                            // For now, just return the first available provider
                            // In a more advanced implementation, we could check capabilities
                            _logger.LogInformation("Using {Provider} for real-time audio", provider);
                            return realtimeClient;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to initialize {Provider} for real-time audio", provider);
                    }
                }

                _logger.LogError("No real-time audio providers available with required capabilities");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing real-time audio request");
                return null;
            }
        }

        /// <summary>
        /// Gets all available transcription providers for a virtual key.
        /// </summary>
        public async Task<List<string>> GetAvailableTranscriptionProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return await GetTranscriptionProvidersAsync();
        }

        /// <summary>
        /// Gets all available TTS providers for a virtual key.
        /// </summary>
        public async Task<List<string>> GetAvailableTextToSpeechProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return await GetTextToSpeechProvidersAsync();
        }

        /// <summary>
        /// Gets all available real-time providers for a virtual key.
        /// </summary>
        public async Task<List<string>> GetAvailableRealtimeProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return await GetRealtimeProvidersAsync();
        }

        /// <summary>
        /// Validates that a specific audio operation can be performed.
        /// </summary>
        public bool ValidateAudioOperation(
            AudioOperation operation,
            string provider,
            AudioRequestBase request,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            // Basic validation
            if (string.IsNullOrEmpty(provider))
            {
                errorMessage = "Provider name is required";
                return false;
            }

            if (request == null)
            {
                errorMessage = "Request cannot be null";
                return false;
            }

            // Validate request
            if (!request.IsValid(out var validationError))
            {
                errorMessage = validationError ?? "Request validation failed";
                return false;
            }

            // Check provider support for operation
            var supportedProviders = operation switch
            {
                AudioOperation.Transcription => GetTranscriptionProvidersAsync().Result,
                AudioOperation.TextToSpeech => GetTextToSpeechProvidersAsync().Result,
                AudioOperation.Realtime => GetRealtimeProvidersAsync().Result,
                _ => new List<string>()
            };

            if (!supportedProviders.Any(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"Provider '{provider}' does not support {operation}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets routing statistics for audio operations.
        /// </summary>
        public async Task<AudioRoutingStatistics> GetRoutingStatisticsAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            // In a real implementation, this would track actual routing statistics
            // For now, return empty statistics
            return await Task.FromResult(new AudioRoutingStatistics
            {
                TranscriptionRequests = 0,
                TextToSpeechRequests = 0,
                RealtimeSessions = 0,
                ProviderStats = new Dictionary<string, ProviderAudioStats>()
            });
        }

        /// <summary>
        /// Gets list of providers that support audio transcription.
        /// </summary>
        private async Task<List<string>> GetTranscriptionProvidersAsync()
        {
            // Known providers that support transcription
            // In a more advanced implementation, this could be dynamic
            return await Task.FromResult(new List<string>
            {
                "openai",      // Whisper API
                "azure",       // Azure OpenAI with Whisper
                "google",      // Google Speech-to-Text
                "vertexai"     // Vertex AI Speech-to-Text
            });
        }

        /// <summary>
        /// Gets list of providers that support text-to-speech.
        /// </summary>
        private async Task<List<string>> GetTextToSpeechProvidersAsync()
        {
            return await Task.FromResult(new List<string>
            {
                "openai",      // OpenAI TTS
                "azure",       // Azure OpenAI TTS
                "elevenlabs",  // ElevenLabs
                "google",      // Google Text-to-Speech
                "vertexai"     // Vertex AI Text-to-Speech
            });
        }

        /// <summary>
        /// Gets list of providers that support real-time audio.
        /// </summary>
        private async Task<List<string>> GetRealtimeProvidersAsync()
        {
            return await Task.FromResult(new List<string>
            {
                "openai",      // OpenAI Realtime API
                "ultravox",    // Ultravox
                "elevenlabs"   // ElevenLabs Conversational AI
            });
        }
    }
}
