using System.Text.Json;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Configuration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Configuration-based implementation of IModelCapabilityService.
    /// Loads model capabilities from configuration files instead of hardcoded values.
    /// Uses hybrid caching (L1: Memory, L2: Redis) for optimal performance and consistency.
    /// </summary>
    public class ConfigurationModelCapabilityService : IModelCapabilityService
    {
        private readonly ILogger<ConfigurationModelCapabilityService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly IOptionsMonitor<ModelConfigurationRoot> _modelConfig;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;
        
        private const string CacheKeyPrefix = "ModelCapability:";
        private readonly TimeSpan _memoryCacheExpiration = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _distributedCacheExpiration = TimeSpan.FromMinutes(60);

        public ConfigurationModelCapabilityService(
            ILogger<ConfigurationModelCapabilityService> logger,
            IMemoryCache memoryCache,
            IOptionsMonitor<ModelConfigurationRoot> modelConfig,
            IDistributedCache? distributedCache = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _modelConfig = modelConfig ?? throw new ArgumentNullException(nameof(modelConfig));
            _distributedCache = distributedCache;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

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


        public async Task<bool> SupportsVideoGenerationAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsVideoGeneration ?? false;
        }

        public async Task<bool> SupportsSpeechToTextAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsSpeechToText ?? false;
        }

        public async Task<bool> SupportsTextToSpeechAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsTextToSpeech ?? false;
        }

        public async Task<bool> SupportsRerankAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.SupportsRerank ?? false;
        }

        public async Task<string?> GetTokenizerTypeAsync(string model)
        {
            var capability = await GetModelCapabilityAsync(model);
            return capability?.TokenizerType;
        }


        public async Task<string?> GetDefaultModelAsync(string provider, string capabilityType)
        {
            var cacheKey = $"{CacheKeyPrefix}Default:{provider}:{capabilityType}";
            
            // Try hybrid cache first
            var cachedResult = await GetFromHybridCacheAsync<string?>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            var config = _modelConfig.CurrentValue;
            var providerDefaults = config.ProviderDefaults.FirstOrDefault(p => 
                p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
            
            if (providerDefaults?.DefaultModels.TryGetValue(capabilityType, out var defaultModel) == true)
            {
                await SetInHybridCacheAsync(cacheKey, defaultModel);
                return defaultModel;
            }
            
            // Fallback: find first enabled model with the capability
            var models = config.Models.Where(m => 
                m.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && 
                m.Enabled);
            
            var result = capabilityType.ToLowerInvariant() switch
            {
                "chat" => models.FirstOrDefault(m => m.Capabilities.SupportsChat)?.ModelId,
                "vision" => models.FirstOrDefault(m => m.Capabilities.SupportsVision)?.ModelId,
                "embeddings" => models.FirstOrDefault(m => m.Capabilities.SupportsEmbeddings)?.ModelId,
                _ => null
            };
            
            await SetInHybridCacheAsync(cacheKey, result);
            return result;
        }

        public async Task RefreshCacheAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing model capability cache");
                
                // Clear memory cache entries by compacting
                if (_memoryCache is MemoryCache mc)
                {
                    mc.Compact(1.0);
                }
                
                // For distributed cache, we'd need to scan for keys with our prefix
                // This is a simplified implementation - Redis keys will expire naturally
                if (_distributedCache != null)
                {
                    _logger.LogInformation("Distributed cache entries will expire naturally - consider implementing Redis SCAN for immediate invalidation");
                }
                
                _logger.LogInformation("Model capability cache refreshed");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Gets a value from hybrid cache (L1: Memory, L2: Redis)
        /// </summary>
        private async Task<T?> GetFromHybridCacheAsync<T>(string key)
        {
            // L1 Cache (Memory) - Fast access
            if (_memoryCache.TryGetValue(key, out T? memoryValue))
            {
                _logger.LogDebug("Memory cache hit for key: {Key}", key);
                return memoryValue;
            }

            // L2 Cache (Redis) - Shared state
            if (_distributedCache != null)
            {
                try
                {
                    var cachedData = await _distributedCache.GetStringAsync(key);
                    if (!string.IsNullOrEmpty(cachedData))
                    {
                        var distributedValue = JsonSerializer.Deserialize<T>(cachedData, _jsonOptions);
                        if (distributedValue != null)
                        {
                            // Populate L1 cache with shorter TTL
                            _memoryCache.Set(key, distributedValue, _memoryCacheExpiration);
                            _logger.LogDebug("Distributed cache hit for key: {Key}", key);
                            return distributedValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving from distributed cache for key: {Key}", key);
                }
            }

            return default(T);
        }

        /// <summary>
        /// Sets a value in hybrid cache (L1: Memory, L2: Redis)
        /// </summary>
        private async Task SetInHybridCacheAsync<T>(string key, T value)
        {
            try
            {
                // Set in distributed cache first
                if (_distributedCache != null)
                {
                    var json = JsonSerializer.Serialize(value, _jsonOptions);
                    await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _distributedCacheExpiration
                    });
                }

                // Set in memory cache with shorter TTL for consistency
                _memoryCache.Set(key, value, _memoryCacheExpiration);
                
                _logger.LogDebug("Set value in hybrid cache for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value in hybrid cache for key: {Key}", key);
                // Still cache in memory as fallback
                _memoryCache.Set(key, value, _memoryCacheExpiration);
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
            
            // Try hybrid cache first
            var cachedResult = await GetFromHybridCacheAsync<Models.Configuration.ModelCapabilities?>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            var config = _modelConfig.CurrentValue;
            var modelConfig = config.Models.FirstOrDefault(m => 
                m.ModelId.Equals(model, StringComparison.OrdinalIgnoreCase) && 
                m.Enabled);
            
            if (modelConfig == null)
            {
                _logger.LogDebug("Model {Model} not found in configuration", model);
                await SetInHybridCacheAsync(cacheKey, (Models.Configuration.ModelCapabilities?)null);
                return null;
            }
            
            _logger.LogDebug("Loaded capabilities for model {Model} from configuration", model);
            await SetInHybridCacheAsync(cacheKey, modelConfig.Capabilities);
            return modelConfig.Capabilities;
        }
    }
}