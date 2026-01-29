using System.Text.Json;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for managing and retrieving model costs, with hybrid caching support (L1: Memory, L2: Redis)
/// </summary>
public class ModelCostService : IModelCostService
{
    private readonly IModelCostRepository _modelCostRepository;
    private readonly IModelProviderMappingRepository _modelProviderMappingRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<ModelCostService> _logger;
    private readonly TimeSpan _memoryCacheDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _distributedCacheDuration = TimeSpan.FromHours(1);
    private readonly JsonSerializerOptions _jsonOptions;
    private const string CacheKeyPrefix = "ModelCost_";
    private const string AllModelsCacheKey = CacheKeyPrefix + "All";

    /// <summary>
    /// Creates a new instance of the ModelCostService
    /// </summary>
    /// <param name="modelCostRepository">The model cost repository</param>
    /// <param name="modelProviderMappingRepository">The model provider mapping repository</param>
    /// <param name="memoryCache">The memory cache</param>
    /// <param name="logger">The logger</param>
    /// <param name="distributedCache">The distributed cache (optional)</param>
    public ModelCostService(
        IModelCostRepository modelCostRepository,
        IModelProviderMappingRepository modelProviderMappingRepository,
        IMemoryCache memoryCache,
        ILogger<ModelCostService> logger,
        IDistributedCache? distributedCache = null)
    {
        _modelCostRepository = modelCostRepository ?? throw new ArgumentNullException(nameof(modelCostRepository));
        _modelProviderMappingRepository = modelProviderMappingRepository ?? throw new ArgumentNullException(nameof(modelProviderMappingRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _distributedCache = distributedCache;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<ModelCost?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID cannot be empty", nameof(modelId));
        }

        try
        {
            string cacheKey = $"{CacheKeyPrefix}{modelId}";

            // Try hybrid cache first
            var cachedCost = await GetFromHybridCacheAsync<ModelCost?>(cacheKey);
            if (cachedCost != null)
            {
                _logger.LogDebug("Cache hit for model cost: {ModelId}", modelId);
                return cachedCost;
            }

            _logger.LogDebug("Cache miss for model cost: {ModelId}, querying database", modelId);

            // Get all model costs with their associated ModelProviderTypeAssociations
            var allCosts = await _modelCostRepository.GetAllAsync(cancellationToken);
            
            // Find a cost where one of its associated ModelProviderTypeAssociations has this identifier
            var now = DateTime.UtcNow;
            var modelCost = allCosts
                .Where(cost => cost.IsActive && cost.EffectiveDate <= now)
                .Where(cost => !cost.ExpiryDate.HasValue || cost.ExpiryDate.Value > now)
                .Where(cost => cost.ModelProviderTypeAssociations.Any(assoc => 
                    assoc.Identifier == modelId && assoc.IsEnabled))
                .OrderByDescending(cost => cost.Priority)
                .ThenByDescending(cost => cost.EffectiveDate)
                .FirstOrDefault();

            if (modelCost == null)
            {
                _logger.LogDebug("No model cost found for identifier: {ModelId}", modelId);
            }

            await SetInHybridCacheAsync(cacheKey, modelCost);
            return modelCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost for model {ModelId}", modelId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ModelCost?> GetCostByIdAsync(int modelCostId, CancellationToken cancellationToken = default)
    {
        try
        {
            string cacheKey = $"{CacheKeyPrefix}Id_{modelCostId}";

            // Try hybrid cache first
            var cachedCost = await GetFromHybridCacheAsync<ModelCost?>(cacheKey);
            if (cachedCost != null)
            {
                _logger.LogDebug("Cache hit for model cost ID: {ModelCostId}", modelCostId);
                return cachedCost;
            }

            _logger.LogDebug("Cache miss for model cost ID: {ModelCostId}, querying database", modelCostId);

            var modelCost = await _modelCostRepository.GetByIdAsync(modelCostId, cancellationToken);

            if (modelCost == null)
            {
                _logger.LogDebug("No model cost found for ID: {ModelCostId}", modelCostId);
                return null;
            }

            // Validate the cost is active and within date range
            var now = DateTime.UtcNow;
            if (!modelCost.IsActive || modelCost.EffectiveDate > now ||
                (modelCost.ExpiryDate.HasValue && modelCost.ExpiryDate.Value <= now))
            {
                _logger.LogDebug("Model cost ID {ModelCostId} exists but is not active or outside date range", modelCostId);
                return null;
            }

            await SetInHybridCacheAsync(cacheKey, modelCost);
            return modelCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost by ID {ModelCostId}", modelCostId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try hybrid cache first
            var cachedCosts = await GetFromHybridCacheAsync<List<ModelCost>?>(AllModelsCacheKey);
            if (cachedCosts != null)
            {
                return cachedCosts;
            }

            var costs = await _modelCostRepository.GetAllAsync(cancellationToken);

            await SetInHybridCacheAsync(AllModelsCacheKey, costs);
            return costs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing model costs");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
    {
        if (modelCost == null)
        {
            throw new ArgumentNullException(nameof(modelCost));
        }

        try
        {
            modelCost.CreatedAt = DateTime.UtcNow;
            modelCost.UpdatedAt = DateTime.UtcNow;

            await _modelCostRepository.CreateAsync(modelCost, cancellationToken);

            // Clear cache
            await ClearCacheAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding model cost {CostName}", modelCost.CostName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
    {
        if (modelCost == null)
        {
            throw new ArgumentNullException(nameof(modelCost));
        }

        try
        {
            var existingCost = await _modelCostRepository.GetByIdAsync(modelCost.Id, cancellationToken);

            if (existingCost == null)
            {
                return false;
            }

            // Update properties
            existingCost.CostName = modelCost.CostName;
            existingCost.InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens;
            existingCost.OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens;
            existingCost.EmbeddingCostPerMillionTokens = modelCost.EmbeddingCostPerMillionTokens;
            existingCost.CachedInputCostPerMillionTokens = modelCost.CachedInputCostPerMillionTokens;
            existingCost.CachedInputWriteCostPerMillionTokens = modelCost.CachedInputWriteCostPerMillionTokens;
            existingCost.UpdatedAt = DateTime.UtcNow;

            bool result = await _modelCostRepository.UpdateAsync(existingCost, cancellationToken);

            // Clear cache
            await ClearCacheAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model cost with ID {ModelCostId}", modelCost.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteModelCostAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            bool result = await _modelCostRepository.DeleteAsync(id, cancellationToken);

            if (result)
            {
                // Clear cache
                await ClearCacheAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model cost with ID {ModelCostId}", id);
            throw;
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
                        _memoryCache.Set(key, distributedValue, _memoryCacheDuration);
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
                    AbsoluteExpirationRelativeToNow = _distributedCacheDuration
                });
            }

            // Set in memory cache with shorter TTL for consistency
            _memoryCache.Set(key, value, _memoryCacheDuration);
            
            _logger.LogDebug("Set value in hybrid cache for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in hybrid cache for key: {Key}", key);
            // Still cache in memory as fallback
            _memoryCache.Set(key, value, _memoryCacheDuration);
        }
    }

    /// <inheritdoc />
    [Obsolete("Use ClearCacheAsync instead. This synchronous method may cause thread pool starvation.")]
    public void ClearCache()
    {
        // Synchronous wrapper for async cache clearing
        // WARNING: This can cause deadlocks in async contexts. Use ClearCacheAsync instead.
#pragma warning disable CA1849 // Call async methods when in an async method
        Task.Run(async () => await ClearCacheAsync()).Wait();
#pragma warning restore CA1849
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        // Remove all ModelCost-related entries from the cache
        _logger.LogInformation("Clearing model cost cache");

        // Clear memory cache entries by compacting
        if (_memoryCache is MemoryCache mc)
        {
            mc.Compact(1.0);
        }

        // For distributed cache, we'd need to scan for keys with our prefix
        // This is a simplified implementation - Redis keys will expire naturally
        if (_distributedCache != null)
        {
            try
            {
                // Remove the known cache key
                await _distributedCache.RemoveAsync(AllModelsCacheKey, cancellationToken);
                _logger.LogInformation("Distributed cache entries cleared (known keys only)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing distributed cache");
            }
        }

        _logger.LogInformation("Model cost cache cleared");
    }
}
