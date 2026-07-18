using System.Text.Json;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service for caching parsed pricing rules configurations using the CacheManager.
/// Reduces JSON parsing overhead by maintaining parsed <see cref="PricingRulesConfig"/> objects in cache.
/// </summary>
public class CachedPricingRulesService : ICachedPricingRulesService
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<CachedPricingRulesService> _logger;

    private const CacheRegion Region = CacheRegion.PricingRules;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Creates a new instance of the CachedPricingRulesService.
    /// </summary>
    /// <param name="cacheManager">The cache manager for L1/L2 caching.</param>
    /// <param name="logger">The logger.</param>
    public CachedPricingRulesService(
        ICacheManager cacheManager,
        ILogger<CachedPricingRulesService> logger)
    {
        _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var cacheKey = CacheKeys.PricingRules.ById(modelCostId);

        try
        {
            // Try cache first
            var cached = await _cacheManager.GetAsync<PricingRulesConfig>(cacheKey, Region, cancellationToken);
            if (cached != null)
            {
                _logger.LogDebug("Cache hit for pricing rules: ModelCostId={ModelCostId}", modelCostId);
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving pricing rules from cache for ModelCostId={ModelCostId}", modelCostId);
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

            // Store in cache
            try
            {
                await _cacheManager.SetAsync(cacheKey, config, Region, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching pricing rules for ModelCostId={ModelCostId}", modelCostId);
            }

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
    public async Task InvalidateCacheAsync(int modelCostId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.PricingRules.ById(modelCostId);

        try
        {
            await _cacheManager.RemoveAsync(cacheKey, Region, cancellationToken);
            _logger.LogInformation("Invalidated pricing rules cache for ModelCostId={ModelCostId}", modelCostId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating pricing rules cache for ModelCostId={ModelCostId}", modelCostId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cacheManager.ClearRegionAsync(Region, cancellationToken);
            _logger.LogInformation("Invalidated all pricing rules cache entries");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating all pricing rules cache entries");
        }
    }
}
