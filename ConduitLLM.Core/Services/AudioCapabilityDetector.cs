using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Default implementation of the audio capability detector.
    /// Uses IModelCapabilityService for database-driven capability detection.
    /// </summary>
    public class AudioCapabilityDetector : IAudioCapabilityDetector
    {
        private readonly ILogger<AudioCapabilityDetector> _logger;
        private readonly IModelCapabilityService _capabilityService;

        /// <summary>
        /// Initializes a new instance of the AudioCapabilityDetector class.
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="capabilityService">Service for retrieving model capabilities from configuration</param>
        public AudioCapabilityDetector(
            ILogger<AudioCapabilityDetector> logger,
            IModelCapabilityService capabilityService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
        }

        /// <summary>
        /// Determines if a provider supports audio transcription.
        /// </summary>
        public bool SupportsTranscription(string provider, string? model = null)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                // Try to get default model for provider
                try
                {
                    model = _capabilityService.GetDefaultModelAsync(provider, "transcription").GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(model))
                    {
                        _logger.LogWarning("No default transcription model found for provider {Provider}", provider);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting default transcription model for provider {Provider}", provider);
                    return false;
                }
            }

            try
            {
                return _capabilityService.SupportsAudioTranscriptionAsync(model).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transcription capability for model {Model}", model);
                return false;
            }
        }

        /// <summary>
        /// Determines if a provider supports text-to-speech synthesis.
        /// </summary>
        public bool SupportsTextToSpeech(string provider, string? model = null)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                // Try to get default model for provider
                try
                {
                    model = _capabilityService.GetDefaultModelAsync(provider, "tts").GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(model))
                    {
                        _logger.LogWarning("No default TTS model found for provider {Provider}", provider);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting default TTS model for provider {Provider}", provider);
                    return false;
                }
            }

            try
            {
                return _capabilityService.SupportsTextToSpeechAsync(model).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking TTS capability for model {Model}", model);
                return false;
            }
        }

        /// <summary>
        /// Determines if a provider supports real-time conversational audio.
        /// </summary>
        public bool SupportsRealtime(string provider, string? model = null)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                // Try to get default model for provider
                try
                {
                    model = _capabilityService.GetDefaultModelAsync(provider, "realtime").GetAwaiter().GetResult();
                    if (string.IsNullOrWhiteSpace(model))
                    {
                        _logger.LogWarning("No default realtime model found for provider {Provider}", provider);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting default realtime model for provider {Provider}", provider);
                    return false;
                }
            }

            try
            {
                return _capabilityService.SupportsRealtimeAudioAsync(model).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking realtime capability for model {Model}", model);
                return false;
            }
        }

        /// <summary>
        /// Checks if a specific voice is available for a provider.
        /// </summary>
        public bool SupportsVoice(string provider, string voiceId)
        {
            try
            {
                // Get default TTS model for provider
                var model = _capabilityService.GetDefaultModelAsync(provider, "tts").GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(model))
                {
                    _logger.LogWarning("No default TTS model found for provider {Provider}", provider);
                    return false;
                }

                // Check if voice is supported by the model
                var supportedVoices = _capabilityService.GetSupportedVoicesAsync(model).GetAwaiter().GetResult();
                return supportedVoices.Contains(voiceId, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking voice support for {Provider}/{Voice}", provider, voiceId);
                return false;
            }
        }

        /// <summary>
        /// Gets the audio formats supported by a provider for a specific operation.
        /// </summary>
        public AudioFormat[] GetSupportedFormats(string provider, AudioOperation operation)
        {
            try
            {
                // Get the appropriate model based on operation
                var capabilityType = operation switch
                {
                    AudioOperation.Transcription => "transcription",
                    AudioOperation.TextToSpeech => "tts",
                    AudioOperation.Realtime => "realtime",
                    _ => null
                };

                if (capabilityType == null)
                {
                    return Array.Empty<AudioFormat>();
                }

                var model = _capabilityService.GetDefaultModelAsync(provider, capabilityType).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(model))
                {
                    _logger.LogWarning("No default {CapabilityType} model found for provider {Provider}", capabilityType, provider);
                    return Array.Empty<AudioFormat>();
                }

                var supportedFormats = _capabilityService.GetSupportedFormatsAsync(model).GetAwaiter().GetResult();
                return supportedFormats
                    .Select(f => Enum.TryParse<AudioFormat>(f, true, out var format) ? format : (AudioFormat?)null)
                    .Where(f => f.HasValue)
                    .Select(f => f!.Value)
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported formats for {Provider}/{Operation}", provider, operation);
                return Array.Empty<AudioFormat>();
            }
        }

        /// <summary>
        /// Gets the languages supported by a provider for a specific audio operation.
        /// </summary>
        public IEnumerable<string> GetSupportedLanguages(string provider, AudioOperation operation)
        {
            try
            {
                // Get the appropriate model based on operation
                var capabilityType = operation switch
                {
                    AudioOperation.Transcription => "transcription",
                    AudioOperation.TextToSpeech => "tts",
                    AudioOperation.Realtime => "realtime",
                    _ => null
                };

                if (capabilityType == null)
                {
                    return Enumerable.Empty<string>();
                }

                var model = _capabilityService.GetDefaultModelAsync(provider, capabilityType).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(model))
                {
                    _logger.LogWarning("No default {CapabilityType} model found for provider {Provider}", capabilityType, provider);
                    return Enumerable.Empty<string>();
                }

                return _capabilityService.GetSupportedLanguagesAsync(model).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported languages for {Provider}/{Operation}", provider, operation);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Validates that an audio request can be processed by the specified provider.
        /// </summary>
        public bool ValidateAudioRequest(AudioRequestBase request, string provider, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!request.IsValid(out var validationError))
            {
                errorMessage = validationError ?? "Request validation failed";
                return false;
            }

            // Additional validation could be added here based on database capabilities

            return true;
        }

        /// <summary>
        /// Gets a list of all providers that support a specific audio capability.
        /// </summary>
        public IEnumerable<string> GetProvidersWithCapability(AudioCapability capability)
        {
            _logger.LogWarning("GetProvidersWithCapability needs to be made async to properly query all models in the capability service");
            // This method needs to be made async to properly query the capability service
            // For now, return known providers based on capability type
            return capability switch
            {
                AudioCapability.BasicTranscription => new[] { "openai", "google", "aws" },
                AudioCapability.TimestampedTranscription => new[] { "openai", "google", "aws" },
                AudioCapability.BasicTTS => new[] { "openai", "google", "aws" },
                AudioCapability.MultiVoiceTTS => new[] { "openai", "google", "aws" },
                AudioCapability.RealtimeConversation => new[] { "openai" },
                AudioCapability.RealtimeFunctions => new[] { "openai" },
                _ => Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Gets detailed capability information for a specific provider.
        /// </summary>
        public AudioProviderCapabilities GetProviderCapabilities(string provider)
        {
            var capabilities = new List<AudioCapability>();
            
            try
            {
                // Check each capability type
                if (SupportsTranscription(provider))
                {
                    capabilities.Add(AudioCapability.BasicTranscription);
                    capabilities.Add(AudioCapability.TimestampedTranscription);
                }
                    
                if (SupportsTextToSpeech(provider))
                {
                    capabilities.Add(AudioCapability.BasicTTS);
                    capabilities.Add(AudioCapability.MultiVoiceTTS);
                }
                    
                if (SupportsRealtime(provider))
                {
                    capabilities.Add(AudioCapability.RealtimeConversation);
                    capabilities.Add(AudioCapability.RealtimeFunctions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting capabilities for provider {Provider}", provider);
            }
            
            return new AudioProviderCapabilities
            {
                Provider = provider,
                DisplayName = provider,
                SupportedCapabilities = capabilities
            };
        }

        /// <summary>
        /// Determines the best provider for a specific audio request.
        /// </summary>
        public string? RecommendProvider(AudioRequestBase request, IEnumerable<string> availableProviders)
        {
            var providers = availableProviders.ToList();
            if (!providers.Any())
                return null;

            // Determine operation type from request
            AudioOperation operation;
            if (request is AudioTranscriptionRequest)
                operation = AudioOperation.Transcription;
            else if (request is TextToSpeechRequest)
                operation = AudioOperation.TextToSpeech;
            // RealtimeSessionConfig is handled separately, not through AudioRequestBase
            else
                return providers.First();

            // Find providers that support the operation
            var capableProviders = providers.Where(p =>
            {
                return operation switch
                {
                    AudioOperation.Transcription => SupportsTranscription(p),
                    AudioOperation.TextToSpeech => SupportsTextToSpeech(p),
                    AudioOperation.Realtime => SupportsRealtime(p),
                    _ => false
                };
            }).ToList();

            // Return first capable provider, or fallback to first available
            return capableProviders.FirstOrDefault() ?? providers.First();
        }
    }
}
