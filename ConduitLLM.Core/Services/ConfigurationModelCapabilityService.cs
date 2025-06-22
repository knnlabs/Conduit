using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Configuration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Configuration-based implementation of IModelCapabilityService.
    /// Loads model capabilities from configuration files instead of hardcoded values.
    /// </summary>
    public class ConfigurationModelCapabilityService : IModelCapabilityService
    {
        private readonly ILogger<ConfigurationModelCapabilityService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IOptionsMonitor<ModelConfigurationRoot> _modelConfig;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        
        private const string CacheKeyPrefix = "ModelCapability:";
        private const int CacheExpirationMinutes = 60;

        public ConfigurationModelCapabilityService(
            ILogger<ConfigurationModelCapabilityService> logger,
            IMemoryCache cache,
            IOptionsMonitor<ModelConfigurationRoot> modelConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _modelConfig = modelConfig ?? throw new ArgumentNullException(nameof(modelConfig));

            // Listen for configuration changes
            _modelConfig.OnChange(_ => 
            {
                _logger.LogInformation("Model configuration changed, clearing cache");
                Task.Run(async () => await RefreshCacheAsync());
            });
        }

        public async Task<bool> SupportsVisionAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsVision ?? false;
        }

        public async Task<bool> SupportsAudioTranscriptionAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsTranscription ?? false;
        }

        public async Task<bool> SupportsTextToSpeechAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsTextToSpeech ?? false;
        }

        public async Task<bool> SupportsRealtimeAudioAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsRealtimeAudio ?? false;
        }

        public async Task<string?> GetTokenizerTypeAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.TokenizerType;
        }

        public async Task<List<string>> GetSupportedVoicesAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportedVoices ?? new List<string>();
        }

        public async Task<List<string>> GetSupportedLanguagesAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportedLanguages ?? new List<string>();
        }

        public async Task<List<string>> GetSupportedFormatsAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportedFormats ?? new List<string>();
        }

        public async Task<string?> GetDefaultModelAsync(string provider, string capabilityType)
        {
            var cacheKey = $"{CacheKeyPrefix}Default:{provider}:{capabilityType}";
            
            return await _cache.GetOrCreateAsync<string?>(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes);
                
                var config = _modelConfig.CurrentValue;
                var providerDefaults = config.ProviderDefaults.FirstOrDefault(p => 
                    p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
                
                if (providerDefaults?.DefaultModels.TryGetValue(capabilityType, out var defaultModel) == true)
                {
                    return Task.FromResult<string?>(defaultModel);
                }
                
                // Fallback: find first enabled model with the capability
                var models = config.Models.Where(m => 
                    m.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && 
                    m.Enabled);
                
                var result = capabilityType.ToLowerInvariant() switch
                {
                    "chat" => models.FirstOrDefault(m => m.Capabilities.SupportsChat)?.ModelId,
                    "vision" => models.FirstOrDefault(m => m.Capabilities.SupportsVision)?.ModelId,
                    "transcription" => models.FirstOrDefault(m => m.Capabilities.SupportsTranscription)?.ModelId,
                    "tts" => models.FirstOrDefault(m => m.Capabilities.SupportsTextToSpeech)?.ModelId,
                    "realtime" => models.FirstOrDefault(m => m.Capabilities.SupportsRealtimeAudio)?.ModelId,
                    "embeddings" => models.FirstOrDefault(m => m.Capabilities.SupportsEmbeddings)?.ModelId,
                    _ => null
                };
                
                return Task.FromResult<string?>(result);
            });
        }

        public async Task RefreshCacheAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing model capability cache");
                
                // Clear all cache entries with our prefix
                var cacheKeys = new List<string>();
                
                // In production, you'd track cache keys or use a distributed cache with pattern support
                // For now, we'll clear specific known patterns
                var config = _modelConfig.CurrentValue;
                foreach (var model in config.Models)
                {
                    _cache.Remove($"{CacheKeyPrefix}Model:{model.ModelId}");
                }
                
                foreach (var provider in config.ProviderDefaults)
                {
                    foreach (var capabilityType in provider.DefaultModels.Keys)
                    {
                        _cache.Remove($"{CacheKeyPrefix}Default:{provider.Provider}:{capabilityType}");
                    }
                }
                
                _logger.LogInformation("Model capability cache refreshed");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task<Models.Configuration.ModelCapabilities?> GetModelCapabilityAsync(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                _logger.LogWarning("Model identifier is null or empty");
                return null;
            }

            var cacheKey = $"{CacheKeyPrefix}Model:{model}";
            
            return await _cache.GetOrCreateAsync<Models.Configuration.ModelCapabilities?>(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes);
                
                var config = _modelConfig.CurrentValue;
                var modelConfig = config.Models.FirstOrDefault(m => 
                    m.ModelId.Equals(model, StringComparison.OrdinalIgnoreCase) && 
                    m.Enabled);
                
                if (modelConfig == null)
                {
                    _logger.LogDebug("Model {Model} not found in configuration", model);
                    return Task.FromResult<Models.Configuration.ModelCapabilities?>(null);
                }
                
                _logger.LogDebug("Loaded capabilities for model {Model} from configuration", model);
                return Task.FromResult<Models.Configuration.ModelCapabilities?>(modelConfig.Capabilities);
            });
        }
    }
}