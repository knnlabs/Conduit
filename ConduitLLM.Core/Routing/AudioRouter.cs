using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Default implementation of the audio router for routing audio requests to appropriate providers.
    /// Uses Provider IDs and database queries instead of hardcoded provider lists.
    /// </summary>
    public class AudioRouter : IAudioRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<AudioRouter> _logger;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly Dictionary<int, AudioRoutingStatistics> _statistics = new();

        public AudioRouter(
            ILLMClientFactory clientFactory,
            ILogger<AudioRouter> logger,
            IModelProviderMappingService modelMappingService)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
        }

        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Model))
                {
                    _logger.LogWarning("No model specified in transcription request");
                    return null;
                }

                // Use the canonical model mapping approach
                var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                if (modelMapping == null)
                {
                    _logger.LogWarning("No model mapping found for transcription model: {Model}", request.Model);
                    return null;
                }

                // Get the client using the standard factory method (which uses model alias)
                var client = _clientFactory.GetClient(request.Model);
                
                if (client is IAudioTranscriptionClient audioClient)
                {
                    // Update the request to use the provider's model ID
                    request.Model = modelMapping.ProviderModelId;
                    
                    _logger.LogInformation(
                        "Routed transcription request to provider: {ProviderId} for model: {Model}", 
                        modelMapping.ProviderId, 
                        modelMapping.ModelAlias);

                    UpdateStatistics(modelMapping.ProviderId);
                    return audioClient;
                }

                _logger.LogWarning("Client for model {Model} does not implement IAudioTranscriptionClient", 
                    modelMapping.ModelAlias);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing transcription request");
                return null;
            }
        }

        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            TextToSpeechRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Model))
                {
                    _logger.LogWarning("No model specified in TTS request");
                    return null;
                }

                // Use the canonical model mapping approach
                var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                if (modelMapping == null)
                {
                    _logger.LogWarning("No model mapping found for TTS model: {Model}", request.Model);
                    return null;
                }

                // Get the client using the standard factory method (which uses model alias)
                var client = _clientFactory.GetClient(request.Model);
                
                if (client is ITextToSpeechClient ttsClient)
                {
                    // Update the request to use the provider's model ID
                    request.Model = modelMapping.ProviderModelId;
                    
                    _logger.LogInformation(
                        "Routed TTS request to provider: {ProviderId} for model: {Model}", 
                        modelMapping.ProviderId, 
                        modelMapping.ModelAlias);

                    UpdateStatistics(modelMapping.ProviderId);
                    return ttsClient;
                }

                _logger.LogWarning("Client for model {Model} does not implement ITextToSpeechClient", 
                    modelMapping.ModelAlias);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing TTS request");
                return null;
            }
        }

        public async Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            RealtimeSessionConfig config,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(config.Model))
                {
                    _logger.LogWarning("No model specified in real-time config");
                    return null;
                }

                // Use the canonical model mapping approach
                var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(config.Model);
                if (modelMapping == null)
                {
                    _logger.LogWarning("No model mapping found for real-time model: {Model}", config.Model);
                    return null;
                }

                // Get the client using the standard factory method (which uses model alias)
                var client = _clientFactory.GetClient(config.Model);
                
                if (client is IRealtimeAudioClient realtimeClient)
                {
                    // Update the config to use the provider's model ID
                    config.Model = modelMapping.ProviderModelId;
                    
                    _logger.LogInformation(
                        "Routed real-time session to provider: {ProviderId} for model: {Model}", 
                        modelMapping.ProviderId, 
                        modelMapping.ModelAlias);

                    UpdateStatistics(modelMapping.ProviderId);
                    return realtimeClient;
                }

                _logger.LogWarning("Client for model {Model} does not implement IRealtimeAudioClient", 
                    modelMapping.ModelAlias);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing real-time request");
                return null;
            }
        }

        public async Task<List<string>> GetAvailableTranscriptionProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            // Get all model mappings that support transcription
            var allMappings = await _modelMappingService.GetAllMappingsAsync();
            var transcriptionModels = allMappings
                .Where(m => m.SupportsAudioTranscription)
                .Select(m => m.ModelAlias)
                .Distinct()
                .ToList();

            return transcriptionModels;
        }

        public async Task<List<string>> GetAvailableTextToSpeechProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            // Get all model mappings that support TTS
            var allMappings = await _modelMappingService.GetAllMappingsAsync();
            var ttsModels = allMappings
                .Where(m => m.SupportsTextToSpeech)
                .Select(m => m.ModelAlias)
                .Distinct()
                .ToList();

            return ttsModels;
        }

        public async Task<List<string>> GetAvailableRealtimeProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            // Get all model mappings that support real-time audio
            var allMappings = await _modelMappingService.GetAllMappingsAsync();
            var realtimeModels = allMappings
                .Where(m => m.SupportsRealtimeAudio)
                .Select(m => m.ModelAlias)
                .Distinct()
                .ToList();

            return realtimeModels;
        }

        public bool ValidateAudioOperation(
            AudioOperation operation,
            string provider,
            AudioRequestBase request,
            out string errorMessage)
        {
            errorMessage = "Provider-based validation not implemented in refactored AudioRouter";
            return false;
        }

        public Task<AudioRoutingStatistics> GetRoutingStatisticsAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            lock (_statistics)
            {
                var combinedStats = new AudioRoutingStatistics();
                if (_statistics.Any())
                {
                    combinedStats.TranscriptionRequests = _statistics.Values.Sum(s => s.TranscriptionRequests);
                    combinedStats.TextToSpeechRequests = _statistics.Values.Sum(s => s.TextToSpeechRequests);
                    combinedStats.RealtimeSessions = _statistics.Values.Sum(s => s.RealtimeSessions);
                    combinedStats.TotalRequests = _statistics.Values.Sum(s => s.TotalRequests);
                    combinedStats.FailedRoutingAttempts = _statistics.Values.Sum(s => s.FailedRoutingAttempts);
                    combinedStats.LastUpdated = DateTime.UtcNow;
                }

                return Task.FromResult(combinedStats);
            }
        }


        /// <summary>
        /// Updates routing statistics for a provider.
        /// </summary>
        private void UpdateStatistics(int providerId)
        {
            lock (_statistics)
            {
                if (!_statistics.ContainsKey(providerId))
                {
                    _statistics[providerId] = new AudioRoutingStatistics
                    {
                        LastUpdated = DateTime.UtcNow
                    };
                }

                _statistics[providerId].TotalRequests++;
                _statistics[providerId].LastUpdated = DateTime.UtcNow;
            }
        }
    }
}
