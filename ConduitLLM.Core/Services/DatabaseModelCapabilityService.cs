using System.Text.Json;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Database-backed implementation of the model capability service.
    /// Retrieves model capabilities from the ModelProviderMapping table.
    /// </summary>
    public class DatabaseModelCapabilityService : IModelCapabilityService
    {
        private readonly ILogger<DatabaseModelCapabilityService> _logger;
        private readonly IModelProviderMappingRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private const string CacheKeyPrefix = "ModelCapability:";

        public DatabaseModelCapabilityService(
            ILogger<DatabaseModelCapabilityService> logger,
            IModelProviderMappingRepository repository,
            IMemoryCache cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsVisionAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Vision:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.ModelProviderTypeAssociation?.Model?.SupportsVision ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking vision capability for model {Model}", model);
                return false;
            }
        }


        /// <inheritdoc/>
        public async Task<bool> SupportsVideoGenerationAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}VideoGeneration:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.ModelProviderTypeAssociation?.Model?.SupportsVideoGeneration ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking video generation capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetTokenizerTypeAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Tokenizer:{model}";
            if (_cache.TryGetValue<string?>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var tokenizerType = mapping?.ModelProviderTypeAssociation?.Model?.TokenizerType;

                // Default to cl100k_base if not specified
                string result = tokenizerType?.ToString() ?? "Cl100KBase";

                _cache.Set(cacheKey, result, _cacheExpiration);
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
            if (_cache.TryGetValue<string?>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                // Default model selection is now deprecated - return null
                // This functionality should be replaced with priority-based routing
                _logger.LogWarning("GetDefaultModelAsync is deprecated. Use priority-based routing instead.");
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
        public Task RefreshCacheAsync()
        {
            // Clear all cached entries with our prefix
            // Note: IMemoryCache doesn't provide a way to clear by prefix,
            // so we'll rely on the expiration timeout for now
            _logger.LogInformation("Model capability cache refresh requested");
            return Task.CompletedTask;
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
                var allMappings = await _repository.GetAllAsync(cancellationToken);
                mapping = allMappings.FirstOrDefault(m =>
                    m.ProviderModelId.Equals(model, StringComparison.OrdinalIgnoreCase));
            }
            return mapping;
        }
    }
}
