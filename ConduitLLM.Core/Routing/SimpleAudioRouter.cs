using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Simplified implementation of the audio router that works with existing interfaces.
    /// </summary>
    public class SimpleAudioRouter : IAudioRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAudioCapabilityDetector _capabilityDetector;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<SimpleAudioRouter> _logger;
        private readonly Dictionary<string, AudioRoutingStatistics> _statistics = new();

        public SimpleAudioRouter(
            ILLMClientFactory clientFactory,
            IAudioCapabilityDetector capabilityDetector,
            IVirtualKeyService virtualKeyService,
            ILogger<SimpleAudioRouter> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate virtual key
                var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
                if (keyEntity == null)
                {
                    _logger.LogWarning("Invalid virtual key provided for audio transcription");
                    return null;
                }

                // Get available providers
                var providers = GetAvailableProviders();

                // Find providers that support transcription
                var transcriptionProviders = providers
                    .Where(p => _capabilityDetector.SupportsTranscription(p))
                    .ToList();

                if (!transcriptionProviders.Any())
                {
                    _logger.LogWarning("No transcription providers available");
                    return null;
                }

                // Use capability detector to recommend best provider
                var selectedProvider = _capabilityDetector.RecommendProvider(request, transcriptionProviders);
                if (selectedProvider == null)
                {
                    selectedProvider = transcriptionProviders.First();
                }

                // Get the client
                var client = _clientFactory.GetClientByProvider(selectedProvider);

                // Verify it implements audio interface
                if (client is IAudioTranscriptionClient audioClient)
                {
                    _logger.LogInformation("Routed transcription request to provider: {Provider}", selectedProvider);
                    return audioClient;
                }

                _logger.LogWarning("Provider {Provider} does not implement IAudioTranscriptionClient", selectedProvider);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing transcription request");
                throw;
            }
        }

        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            TextToSpeechRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate virtual key
                var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
                if (keyEntity == null)
                {
                    _logger.LogWarning("Invalid virtual key provided for TTS");
                    return null;
                }

                // Get available providers
                var providers = GetAvailableProviders();

                // Find providers that support TTS and the requested voice
                var ttsProviders = providers
                    .Where(p => _capabilityDetector.SupportsTextToSpeech(p))
                    .Where(p => _capabilityDetector.SupportsVoice(p, request.Voice))
                    .ToList();

                if (!ttsProviders.Any())
                {
                    _logger.LogWarning("No TTS providers available for voice: {Voice}", request.Voice);
                    return null;
                }

                // Use capability detector to recommend best provider
                var selectedProvider = _capabilityDetector.RecommendProvider(request, ttsProviders);
                if (selectedProvider == null)
                {
                    selectedProvider = ttsProviders.First();
                }

                // Get the client
                var client = _clientFactory.GetClientByProvider(selectedProvider);

                // Verify it implements audio interface
                if (client is ITextToSpeechClient ttsClient)
                {
                    _logger.LogInformation("Routed TTS request to provider: {Provider}", selectedProvider);
                    return ttsClient;
                }

                _logger.LogWarning("Provider {Provider} does not implement ITextToSpeechClient", selectedProvider);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing TTS request");
                throw;
            }
        }

        public async Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            RealtimeSessionConfig config,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate virtual key
                var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
                if (keyEntity == null)
                {
                    _logger.LogWarning("Invalid virtual key provided for real-time audio");
                    return null;
                }

                // Get available providers
                var providers = GetAvailableProviders();

                // Find providers that support real-time
                var realtimeProviders = providers
                    .Where(p => _capabilityDetector.SupportsRealtime(p))
                    .Where(p => _capabilityDetector.SupportsVoice(p, config.Voice))
                    .ToList();

                if (!realtimeProviders.Any())
                {
                    _logger.LogWarning("No real-time providers available");
                    return null;
                }

                var selectedProvider = realtimeProviders.First();

                // Get the client
                var client = _clientFactory.GetClientByProvider(selectedProvider);

                // Verify it implements real-time interface
                if (client is IRealtimeAudioClient realtimeClient)
                {
                    _logger.LogInformation("Routed real-time session to provider: {Provider}", selectedProvider);
                    return realtimeClient;
                }

                _logger.LogWarning("Provider {Provider} does not implement IRealtimeAudioClient", selectedProvider);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing real-time request");
                throw;
            }
        }

        public Task<List<string>> GetAvailableTranscriptionProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providers = GetAvailableProviders()
                .Where(p => _capabilityDetector.SupportsTranscription(p))
                .ToList();

            return Task.FromResult(providers);
        }

        public Task<List<string>> GetAvailableTextToSpeechProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providers = GetAvailableProviders()
                .Where(p => _capabilityDetector.SupportsTextToSpeech(p))
                .ToList();

            return Task.FromResult(providers);
        }

        public Task<List<string>> GetAvailableRealtimeProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var providers = GetAvailableProviders()
                .Where(p => _capabilityDetector.SupportsRealtime(p))
                .ToList();

            return Task.FromResult(providers);
        }

        public bool ValidateAudioOperation(
            AudioOperation operation,
            string provider,
            AudioRequestBase request,
            out string errorMessage)
        {
            return _capabilityDetector.ValidateAudioRequest(request, provider, out errorMessage);
        }

        public Task<AudioRoutingStatistics> GetRoutingStatisticsAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            lock (_statistics)
            {
                if (_statistics.TryGetValue(virtualKey, out var stats))
                {
                    return Task.FromResult(stats);
                }

                return Task.FromResult(new AudioRoutingStatistics());
            }
        }

        private List<string> GetAvailableProviders()
        {
            // Hardcoded for now - in reality this would come from configuration
            return new List<string> { "openai", "azure" };
        }
    }
}
