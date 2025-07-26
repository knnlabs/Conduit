using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Basic implementation of the simple audio router.
    /// </summary>
    public class BasicAudioRouter : ISimpleAudioRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAudioCapabilityDetector _capabilityDetector;
        private readonly Core.Interfaces.Configuration.IProviderCredentialService _providerCredentialService;
        private readonly ILogger<BasicAudioRouter> _logger;

        public BasicAudioRouter(
            ILLMClientFactory clientFactory,
            IAudioCapabilityDetector capabilityDetector,
            Core.Interfaces.Configuration.IProviderCredentialService providerCredentialService,
            ILogger<BasicAudioRouter> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
            _providerCredentialService = providerCredentialService ?? throw new ArgumentNullException(nameof(providerCredentialService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Since the Core layer doesn't have access to all credentials,
                // we need to check known audio provider IDs directly
                // These IDs correspond to the ProviderType enum values
                var audioProviderIds = new[] 
                { 
                    1,  // OpenAI
                    3,  // AzureOpenAI
                    4,  // Gemini
                    5,  // VertexAI
                    20  // GoogleCloud
                };
                
                foreach (var providerId in audioProviderIds)
                {
                    try
                    {
                        var credentials = await _providerCredentialService.GetCredentialByIdAsync(providerId);
                        if (credentials == null || !credentials.IsEnabled)
                            continue;

                        // Check if provider supports transcription
                        var providerName = GetProviderNameFromId(providerId);
                        if (!_capabilityDetector.SupportsTranscription(providerName))
                            continue;

                        // Get client by provider ID
                        var client = _clientFactory.GetClientByProviderId(providerId);
                        if (client is IAudioTranscriptionClient audioClient)
                        {
                            _logger.LogInformation("Selected provider {ProviderName} (ID: {ProviderId}) for audio transcription", 
                                providerName, providerId);
                            return audioClient;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get client from provider ID {ProviderId}", providerId);
                    }
                }

                _logger.LogWarning("No transcription providers available");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transcription client");
                return null;
            }
        }

        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            string? voice = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // TTS provider IDs
                var ttsProviderIds = new[] 
                { 
                    1,   // OpenAI
                    3,   // AzureOpenAI
                    19,  // ElevenLabs
                    4,   // Gemini
                    5,   // VertexAI
                    20   // GoogleCloud
                };

                foreach (var providerId in ttsProviderIds)
                {
                    try
                    {
                        var credentials = await _providerCredentialService.GetCredentialByIdAsync(providerId);
                        if (credentials == null || !credentials.IsEnabled)
                            continue;

                        // Check if provider supports TTS
                        var providerName = GetProviderNameFromId(providerId);
                        if (!_capabilityDetector.SupportsTextToSpeech(providerName))
                            continue;

                        // Get client by provider ID
                        var client = _clientFactory.GetClientByProviderId(providerId);
                        if (client is ITextToSpeechClient ttsClient)
                        {
                            // If voice is specified, check if provider supports it
                            if (!string.IsNullOrEmpty(voice))
                            {
                                try
                                {
                                    var voices = await ttsClient.ListVoicesAsync(cancellationToken: cancellationToken);
                                    if (!voices.Any(v => v.VoiceId == voice || v.Name == voice))
                                    {
                                        continue; // Try next provider
                                    }
                                }
                                catch
                                {
                                    // Continue with this provider anyway
                                }
                            }

                            _logger.LogInformation("Selected provider {ProviderName} (ID: {ProviderId}) for text-to-speech", 
                                providerName, providerId);
                            return ttsClient;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get client from provider ID {ProviderId}", providerId);
                    }
                }

                _logger.LogWarning("No TTS providers available");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TTS client");
                return null;
            }
        }

        private string GetProviderNameFromId(int providerId)
        {
            // Map provider ID to name based on ProviderType enum
            return ((ProviderType)providerId).ToString();
        }
    }
}
