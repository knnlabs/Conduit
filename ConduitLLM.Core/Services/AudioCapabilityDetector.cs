using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

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
                _logger.LogWarning("No model specified for transcription check, returning false");
                return false;
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
                _logger.LogWarning("No model specified for TTS check, returning false");
                return false;
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
                _logger.LogWarning("No model specified for realtime check, returning false");
                return false;
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
            // Voice support is determined by the provider's TTS models
            // This would need to be enhanced to check against the database's SupportedVoices field
            _logger.LogDebug("Voice support check for {Provider}/{Voice} - returning true for now", provider, voiceId);
            return true; // Simplified for now
        }

        /// <summary>
        /// Gets the audio formats supported by a provider for a specific operation.
        /// </summary>
        public AudioFormat[] GetSupportedFormats(string provider, AudioOperation operation)
        {
            // This would need to query the database for supported formats
            // For now, return a basic set of commonly supported formats
            return operation switch
            {
                AudioOperation.Transcription => new[] { AudioFormat.Mp3, AudioFormat.Wav, AudioFormat.M4a, AudioFormat.Webm },
                AudioOperation.TextToSpeech => new[] { AudioFormat.Mp3, AudioFormat.Wav, AudioFormat.Opus },
                AudioOperation.Realtime => new[] { AudioFormat.Pcm },
                _ => Array.Empty<AudioFormat>()
            };
        }

        /// <summary>
        /// Gets the languages supported by a provider for a specific audio operation.
        /// </summary>
        public IEnumerable<string> GetSupportedLanguages(string provider, AudioOperation operation)
        {
            // This would need to query the database for supported languages
            // For now, return a basic set of commonly supported languages
            return new[] { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko" };
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
            // This would need to be implemented by querying the database
            // For now, return empty list
            _logger.LogWarning("GetProvidersWithCapability not fully implemented, returning empty list");
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets detailed capability information for a specific provider.
        /// </summary>
        public AudioProviderCapabilities GetProviderCapabilities(string provider)
        {
            // This would need to be implemented by querying the database
            // For now, return basic capabilities
            return new AudioProviderCapabilities 
            { 
                Provider = provider,
                DisplayName = provider,
                SupportedCapabilities = new List<AudioCapability>()
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

            // For now, return the first available provider
            // This could be enhanced to consider model capabilities from the database
            return providers.First();
        }
    }
}