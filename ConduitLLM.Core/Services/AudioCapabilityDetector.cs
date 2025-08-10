using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Default implementation of the audio capability detector.
    /// Uses Provider IDs and ProviderType to determine capabilities.
    /// </summary>
    public class AudioCapabilityDetector : IAudioCapabilityDetector
    {
        private readonly ILogger<AudioCapabilityDetector> _logger;
        private readonly IModelCapabilityService _capabilityService;
        private readonly IProviderService _providerService;

        /// <summary>
        /// Initializes a new instance of the AudioCapabilityDetector class.
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="capabilityService">Service for retrieving model capabilities from configuration</param>
        /// <param name="providerService">Service for retrieving provider information</param>
        public AudioCapabilityDetector(
            ILogger<AudioCapabilityDetector> logger,
            IModelCapabilityService capabilityService,
            IProviderService providerService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
            _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        }

        /// <summary>
        /// Determines if a provider supports audio transcription.
        /// </summary>
        public bool SupportsTranscription(int providerId, string? model = null)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null || !provider.IsEnabled)
                {
                    return false;
                }

                return provider.ProviderType switch
                {
                    ProviderType.OpenAI => true,
                    ProviderType.Groq => true,
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transcription capability for provider {ProviderId}", providerId);
                return false;
            }
        }

        /// <summary>
        /// Determines if a provider supports text-to-speech synthesis.
        /// </summary>
        public bool SupportsTextToSpeech(int providerId, string? model = null)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null || !provider.IsEnabled)
                {
                    return false;
                }

                return provider.ProviderType switch
                {
                    ProviderType.OpenAI => true,
                    ProviderType.ElevenLabs => true,
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking text-to-speech capability for provider {ProviderId}", providerId);
                return false;
            }
        }

        /// <summary>
        /// Determines if a provider supports real-time conversational audio.
        /// </summary>
        public bool SupportsRealtime(int providerId, string? model = null)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null || !provider.IsEnabled)
                {
                    return false;
                }

                return provider.ProviderType switch
                {
                    ProviderType.OpenAI => true,
                    ProviderType.ElevenLabs => true,
                    ProviderType.Ultravox => true,
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking realtime capability for provider {ProviderId}", providerId);
                return false;
            }
        }

        /// <summary>
        /// Checks if a specific voice is available for a provider.
        /// </summary>
        public bool SupportsVoice(int providerId, string voiceId)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null || !provider.IsEnabled)
                {
                    return false;
                }

                // Basic implementation - could be enhanced with provider-specific voice validation
                return SupportsTextToSpeech(providerId) || SupportsRealtime(providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking voice support for provider {ProviderId}, voice {VoiceId}", providerId, voiceId);
                return false;
            }
        }

        /// <summary>
        /// Gets the audio formats supported by a provider for a specific operation.
        /// </summary>
        public AudioFormat[] GetSupportedFormats(int providerId, AudioOperation operation)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null || !provider.IsEnabled)
                {
                    return Array.Empty<AudioFormat>();
                }

                // Basic implementation - return common formats
                return provider.ProviderType switch
                {
                    ProviderType.OpenAI => new[] { AudioFormat.Mp3, AudioFormat.Wav, AudioFormat.Flac, AudioFormat.Ogg },
                    ProviderType.Groq => new[] { AudioFormat.Mp3, AudioFormat.Wav, AudioFormat.Flac },
                    ProviderType.ElevenLabs => new[] { AudioFormat.Mp3, AudioFormat.Wav },
                    _ => Array.Empty<AudioFormat>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported formats for provider {ProviderId}, operation {Operation}", providerId, operation);
                return Array.Empty<AudioFormat>();
            }
        }

        /// <summary>
        /// Gets the languages supported by a provider for a specific audio operation.
        /// </summary>
        public IEnumerable<string> GetSupportedLanguages(int providerId, AudioOperation operation)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null || !provider.IsEnabled)
                {
                    return Enumerable.Empty<string>();
                }

                // Basic implementation - return common languages
                return new[] { "en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported languages for provider {ProviderId}, operation {Operation}", providerId, operation);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Validates that an audio request can be processed by the specified provider.
        /// </summary>
        public bool ValidateAudioRequest(AudioRequestBase request, int providerId, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null)
                {
                    errorMessage = $"Provider with ID {providerId} not found";
                    return false;
                }

                if (!provider.IsEnabled)
                {
                    errorMessage = $"Provider {provider.ProviderName} is disabled";
                    return false;
                }

                // Basic validation - could be enhanced with more specific checks
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating audio request for provider {ProviderId}", providerId);
                errorMessage = "Internal error validating request";
                return false;
            }
        }

        /// <summary>
        /// Gets a list of all provider IDs that support a specific audio capability.
        /// </summary>
        public IEnumerable<int> GetProvidersWithCapability(AudioCapability capability)
        {
            try
            {
                var allProviders = _providerService.GetAllEnabledProvidersAsync().GetAwaiter().GetResult();
                
                return capability switch
                {
                    AudioCapability.BasicTranscription => allProviders.Where(p => SupportsTranscription(p.Id)).Select(p => p.Id),
                    AudioCapability.BasicTTS => allProviders.Where(p => SupportsTextToSpeech(p.Id)).Select(p => p.Id),
                    AudioCapability.RealtimeConversation => allProviders.Where(p => SupportsRealtime(p.Id)).Select(p => p.Id),
                    _ => Enumerable.Empty<int>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting providers with capability {Capability}", capability);
                return Enumerable.Empty<int>();
            }
        }

        /// <summary>
        /// Gets detailed capability information for a specific provider.
        /// </summary>
        public AudioProviderCapabilities GetProviderCapabilities(int providerId)
        {
            try
            {
                var provider = _providerService.GetByIdAsync(providerId).GetAwaiter().GetResult();
                if (provider == null)
                {
                    return new AudioProviderCapabilities();
                }

                return new AudioProviderCapabilities
                {
                    Provider = providerId.ToString(),
                    DisplayName = provider.ProviderName,
                    SupportedCapabilities = new List<AudioCapability>(),
                    TextToSpeech = new TextToSpeechCapabilities
                    {
                        SupportedFormats = GetSupportedFormats(providerId, AudioOperation.TextToSpeech).ToList(),
                        SupportedLanguages = GetSupportedLanguages(providerId, AudioOperation.TextToSpeech).ToList()
                    },
                    Transcription = new TranscriptionCapabilities
                    {
                        SupportedLanguages = GetSupportedLanguages(providerId, AudioOperation.Transcription).ToList()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting capabilities for provider {ProviderId}", providerId);
                return new AudioProviderCapabilities();
            }
        }

        /// <summary>
        /// Determines the best provider for a specific audio request based on capabilities and requirements.
        /// </summary>
        public int? RecommendProvider(AudioRequestBase request, IEnumerable<int> availableProviderIds)
        {
            try
            {
                var candidates = availableProviderIds.ToList();
                if (!candidates.Any())
                {
                    return null;
                }

                // Simple recommendation logic - return first capable provider
                // Could be enhanced with more sophisticated selection criteria
                return candidates.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recommending provider for request");
                return null;
            }
        }
    }
}