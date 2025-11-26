using System.Text.Json;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Pricing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service for caching parsed pricing rules configurations with hybrid cache support (L1: Memory, L2: Redis).
/// Reduces JSON parsing overhead by maintaining parsed <see cref="PricingRulesConfig"/> objects in cache.
/// </summary>
public class CachedPricingRulesService : ICachedPricingRulesService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<CachedPricingRulesService> _logger;

    private readonly TimeSpan _memoryCacheDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _distributedCacheDuration = TimeSpan.FromHours(1);
    private const string CacheKeyPrefix = "PricingRules_";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Creates a new instance of the CachedPricingRulesService.
    /// </summary>
    /// <param name="memoryCache">The memory cache for L1 caching.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="distributedCache">Optional distributed cache for L2 caching (Redis).</param>
    public CachedPricingRulesService(
        IMemoryCache memoryCache,
        ILogger<CachedPricingRulesService> logger,
        IDistributedCache? distributedCache = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _distributedCache = distributedCache;
    }

    /// <inheritdoc />
    public async Task<PricingRulesConfig?> GetConfigAsync(
        int modelCostId,
        string pricingConfiguration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(pricingConfiguration))
        {
            _logger.LogWarning("Empty pricing configuration for ModelCost {ModelCostId}", modelCostId);
            return null;
        }

        var cacheKey = $"{CacheKeyPrefix}{modelCostId}";

        // Try L1 cache (memory) first
        if (_memoryCache.TryGetValue(cacheKey, out PricingRulesConfig? memoryCached))
        {
            _logger.LogDebug("Memory cache hit for pricing rules: ModelCostId={ModelCostId}", modelCostId);
            return memoryCached;
        }

        // Try L2 cache (distributed/Redis) if available
        if (_distributedCache != null)
        {
            try
            {
                var distributedData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrEmpty(distributedData))
                {
                    var distributedConfig = JsonSerializer.Deserialize<PricingRulesConfig>(distributedData, JsonOptions);
                    if (distributedConfig != null)
                    {
                        // Populate L1 cache for faster subsequent access
                        _memoryCache.Set(cacheKey, distributedConfig, _memoryCacheDuration);
                        _logger.LogDebug("Distributed cache hit for pricing rules: ModelCostId={ModelCostId}", modelCostId);
                        return distributedConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving pricing rules from distributed cache for ModelCostId={ModelCostId}", modelCostId);
            }
        }

        // Cache miss - parse the configuration
        _logger.LogDebug("Cache miss for pricing rules: ModelCostId={ModelCostId}, parsing configuration", modelCostId);

        try
        {
            var config = JsonSerializer.Deserialize<PricingRulesConfig>(pricingConfiguration, JsonOptions);
            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize pricing rules configuration for ModelCostId={ModelCostId}", modelCostId);
                return null;
            }

            // Store in both caches
            await SetInCacheAsync(cacheKey, config, cancellationToken);

            _logger.LogDebug("Parsed and cached pricing rules for ModelCostId={ModelCostId}", modelCostId);
            return config;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in pricing rules configuration for ModelCostId={ModelCostId}", modelCostId);
            return null;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(int modelCostId)
    {
        var cacheKey = $"{CacheKeyPrefix}{modelCostId}";

        // Remove from memory cache
        _memoryCache.Remove(cacheKey);

        // Remove from distributed cache (fire-and-forget)
        if (_distributedCache != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _distributedCache.RemoveAsync(cacheKey);
                    _logger.LogDebug("Invalidated distributed cache for pricing rules: ModelCostId={ModelCostId}", modelCostId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating distributed cache for pricing rules: ModelCostId={ModelCostId}", modelCostId);
                }
            });
        }

        _logger.LogInformation("Invalidated pricing rules cache for ModelCostId={ModelCostId}", modelCostId);
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        // For memory cache, we can't easily invalidate by prefix without tracking keys
        // The recommended approach is to use cache entry options with a linked token
        // For now, we'll compact the memory cache which forces eviction evaluation
        if (_memoryCache is MemoryCache mc)
        {
            mc.Compact(1.0);
        }

        _logger.LogInformation("Invalidated all pricing rules cache entries (memory cache compacted)");

        // Note: For distributed cache, we'd need Redis SCAN + DEL which is expensive
        // Individual cache entries will expire naturally based on TTL
    }

    /// <summary>
    /// Sets a configuration in both L1 (memory) and L2 (distributed) caches.
    /// </summary>
    private async Task SetInCacheAsync(string cacheKey, PricingRulesConfig config, CancellationToken cancellationToken)
    {
        // Set in memory cache (L1)
        _memoryCache.Set(cacheKey, config, _memoryCacheDuration);

        // Set in distributed cache (L2) if available
        if (_distributedCache != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, JsonOptions);
                await _distributedCache.SetStringAsync(
                    cacheKey,
                    json,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _distributedCacheDuration
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error setting pricing rules in distributed cache for key={CacheKey}", cacheKey);
                // Continue - memory cache is still populated
            }
        }
    }
}
