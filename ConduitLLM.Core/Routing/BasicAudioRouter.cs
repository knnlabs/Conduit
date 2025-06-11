using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Basic implementation of the simple audio router.
    /// </summary>
    public class BasicAudioRouter : ISimpleAudioRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAudioCapabilityDetector _capabilityDetector;
        private readonly ILogger<BasicAudioRouter> _logger;

        public BasicAudioRouter(
            ILLMClientFactory clientFactory,
            IAudioCapabilityDetector capabilityDetector,
            ILogger<BasicAudioRouter> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get available providers
                var providers = new[] { "openai", "azure", "google", "vertexai" };
                
                // Find providers that support transcription
                var transcriptionProviders = providers
                    .Where(p => _capabilityDetector.SupportsTranscription(p))
                    .ToList();

                if (!transcriptionProviders.Any())
                {
                    _logger.LogWarning("No transcription providers available");
                    return Task.FromResult<IAudioTranscriptionClient?>(null);
                }

                // Try each provider
                foreach (var provider in transcriptionProviders)
                {
                    try
                    {
                        var client = _clientFactory.GetClientByProvider(provider);
                        if (client is IAudioTranscriptionClient audioClient)
                        {
                            _logger.LogInformation("Selected {Provider} for audio transcription", provider);
                            return Task.FromResult<IAudioTranscriptionClient?>(audioClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get client from provider {Provider}", provider);
                    }
                }

                return Task.FromResult<IAudioTranscriptionClient?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transcription client");
                return Task.FromResult<IAudioTranscriptionClient?>(null);
            }
        }

        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            string? voice = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get available providers
                var providers = new[] { "openai", "azure", "elevenlabs", "google", "vertexai" };
                
                // Find providers that support TTS
                var ttsProviders = providers
                    .Where(p => _capabilityDetector.SupportsTextToSpeech(p))
                    .ToList();

                if (!ttsProviders.Any())
                {
                    _logger.LogWarning("No TTS providers available");
                    return null;
                }

                // Try each provider
                foreach (var provider in ttsProviders)
                {
                    try
                    {
                        var client = _clientFactory.GetClientByProvider(provider);
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

                            _logger.LogInformation("Selected {Provider} for text-to-speech", provider);
                            return ttsClient;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get client from provider {Provider}", provider);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting TTS client");
                return null;
            }
        }
    }
}