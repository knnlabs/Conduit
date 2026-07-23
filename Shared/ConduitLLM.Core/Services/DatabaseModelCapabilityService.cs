using System.Text.Json;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Models;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Database-backed implementation of the model capability service.
    /// Retrieves model capabilities from the ModelProviderMapping table.
    /// Uses hybrid caching (L1: Memory, L2: Redis) for optimal performance and consistency.
    /// </summary>
    public class DatabaseModelCapabilityService : IModelCapabilityService
    {
        private readonly ILogger<DatabaseModelCapabilityService> _logger;
        private readonly IModelProviderMappingRepository _repository;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly TimeSpan _memoryCacheExpiration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _distributedCacheExpiration = TimeSpan.FromMinutes(30);
        private const string CacheKeyPrefix = "ModelCapability:";
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> KnownCacheKeys = new();
        private readonly JsonSerializerOptions _jsonOptions;

        public DatabaseModelCapabilityService(
            ILogger<DatabaseModelCapabilityService> logger,
            IModelProviderMappingRepository repository,
            IMemoryCache memoryCache,
            IDistributedCache? distributedCache = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _distributedCache = distributedCache;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsVisionAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Vision:{model}";
            
            // Try hybrid cache first
            var cachedResult = await GetFromHybridCacheAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
            {
                return cachedResult.Value;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var association = mapping?.ModelProviderTypeAssociation;
                var result = association?.Model is not null &&
                    ModelCapabilityResolver.Resolve(association.Model, association).SupportsImageInput;
                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking vision capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsVideoInputAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}VideoInput:{model}";

            var cachedResult = await GetFromHybridCacheAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
            {
                return cachedResult.Value;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var association = mapping?.ModelProviderTypeAssociation;
                var result = association?.Model is not null &&
                    ModelCapabilityResolver.Resolve(association.Model, association).SupportsVideoInput;
                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking video input capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsVideoGenerationAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}VideoGeneration:{model}";
            
            // Try hybrid cache first
            var cachedResult = await GetFromHybridCacheAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
            {
                return cachedResult.Value;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var association = mapping?.ModelProviderTypeAssociation;
                var result = association?.Model is not null &&
                    ModelCapabilityResolver.Resolve(association.Model, association).SupportsVideoGeneration;
                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking video generation capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsSpeechToTextAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}SpeechToText:{model}";

            var cachedResult = await GetFromHybridCacheAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
            {
                return cachedResult.Value;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var association = mapping?.ModelProviderTypeAssociation;
                var result = association?.Model is not null &&
                    ModelCapabilityResolver.Resolve(association.Model, association).SupportsSpeechToText;
                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking speech-to-text capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsTextToSpeechAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}TextToSpeech:{model}";

            var cachedResult = await GetFromHybridCacheAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
            {
                return cachedResult.Value;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var association = mapping?.ModelProviderTypeAssociation;
                var result = association?.Model is not null &&
                    ModelCapabilityResolver.Resolve(association.Model, association).SupportsTextToSpeech;
                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking text-to-speech capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsRerankAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Rerank:{model}";

            var cachedResult = await GetFromHybridCacheAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
            {
                return cachedResult.Value;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var association = mapping?.ModelProviderTypeAssociation;
                var result = association?.Model is not null &&
                    ModelCapabilityResolver.Resolve(association.Model, association).SupportsRerank;
                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rerank capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetTokenizerTypeAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Tokenizer:{model}";
            
            // Try hybrid cache first
            var cachedResult = await GetFromHybridCacheAsync<string?>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var tokenizerType = mapping?.ModelProviderTypeAssociation?.Model?.TokenizerType;

                // Default to cl100k_base if not specified
                string result = tokenizerType?.ToString() ?? "Cl100KBase";

                await SetInHybridCacheAsync(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tokenizer type for model {Model}", model);
                return "Cl100KBase"; // Default fallback
            }
        }


        /// <inheritdoc/>
        public async Task<string?> GetDefaultModelAsync(string provider, string capabilityType)
        {
            var cacheKey = $"{CacheKeyPrefix}Default:{provider}:{capabilityType}";
            
            // Try hybrid cache first
            var cachedResult = await GetFromHybridCacheAsync<string?>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                // Default model selection is now deprecated - return null
                // This functionality should be replaced with priority-based routing
                _logger.LogWarning("GetDefaultModelAsync is deprecated. Use priority-based routing instead.");
                await SetInHybridCacheAsync(cacheKey, (string?)null);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default model for provider {Provider} and capability {Capability}",
                    provider, capabilityType);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task RefreshCacheAsync()
        {
            foreach (var cacheKey in KnownCacheKeys.Keys)
            {
                _memoryCache.Remove(cacheKey);
                if (_distributedCache is not null)
                {
                    await _distributedCache.RemoveAsync(cacheKey);
                }
            }

            _logger.LogInformation("Model capability cache refresh completed");
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
                KnownCacheKeys.TryAdd(key, 0);
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

        /// <summary>
        /// Helper method to get a mapping by model name, checking both alias and provider model name.
        /// </summary>
        private async Task<ModelProviderMapping?> GetMappingByModelNameAsync(string model, CancellationToken cancellationToken = default)
        {
            var mapping = await _repository.GetByModelNameAsync(model, cancellationToken);
            if (mapping == null)
            {
                // Try to find by provider model name
                var allMappings = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _repository.GetPaginatedAsync, cancellationToken: cancellationToken);
                mapping = allMappings.FirstOrDefault(m =>
                    m.ProviderModelId.Equals(model, StringComparison.OrdinalIgnoreCase));
            }
            return mapping;
        }
    }
}
