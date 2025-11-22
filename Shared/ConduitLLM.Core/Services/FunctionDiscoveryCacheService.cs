using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Implementation of function discovery cache service using CacheManager for unified cache management.
/// Caches tool definitions with per-function TTL configuration and global enable/disable toggle.
/// </summary>
public class FunctionDiscoveryCacheService : IFunctionDiscoveryCacheService
{
    private readonly ICacheManager _cacheManager;
    private readonly IFunctionConfigurationRepository _functionConfigRepository;
    private readonly IGlobalSettingRepository _globalSettingRepository;
    private readonly ILogger<FunctionDiscoveryCacheService> _logger;

    // Statistics tracking
    private long _totalHits;
    private long _totalMisses;
    private DateTime? _lastInvalidation;

    private const CacheRegion FUNCTION_DISCOVERY_REGION = CacheRegion.FunctionDiscovery;
    private const string GLOBAL_SETTING_KEY = "Functions.DiscoveryCacheEnabled";
    private const int DEFAULT_TTL_MINUTES = 60; // Default if function has no TTL set

    public FunctionDiscoveryCacheService(
        ICacheManager cacheManager,
        IFunctionConfigurationRepository functionConfigRepository,
        IGlobalSettingRepository globalSettingRepository,
        ILogger<FunctionDiscoveryCacheService> logger)
    {
        _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
        _functionConfigRepository = functionConfigRepository ?? throw new ArgumentNullException(nameof(functionConfigRepository));
        _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("FunctionDiscoveryCacheService initialized using CacheManager with FunctionDiscovery region");
    }

    public async Task<bool> IsCachingEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var setting = await _globalSettingRepository.GetByKeyAsync(GLOBAL_SETTING_KEY, cancellationToken);

            // Parse boolean value (true/false, 1/0, yes/no)
            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                return false; // Default to disabled if setting doesn't exist
            }

            return setting.Value.Trim().ToLowerInvariant() switch
            {
                "true" or "1" or "yes" or "enabled" => true,
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if function discovery caching is enabled. Defaulting to disabled.");
            return false;
        }
    }

    public async Task<List<Tool>?> GetCachedToolsAsync(
        List<int> functionConfigurationIds,
        CancellationToken cancellationToken = default)
    {
        // Check global enable/disable setting first
        if (!await IsCachingEnabledAsync(cancellationToken))
        {
            _logger.LogDebug("Function discovery caching is globally disabled");
            return null;
        }

        if (functionConfigurationIds == null || functionConfigurationIds.Count == 0)
        {
            return null;
        }

        var cacheKey = BuildCacheKey(functionConfigurationIds);

        try
        {
            var result = await _cacheManager.GetAsync<List<Tool>>(cacheKey, FUNCTION_DISCOVERY_REGION, cancellationToken);

            if (result != null)
            {
                Interlocked.Increment(ref _totalHits);
                _logger.LogDebug("Function discovery cache hit for key: {CacheKey} ({Count} function configs)",
                    cacheKey, functionConfigurationIds.Count);
            }
            else
            {
                Interlocked.Increment(ref _totalMisses);
                _logger.LogDebug("Function discovery cache miss for key: {CacheKey}", cacheKey);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving function discovery results from cache for key: {CacheKey}", cacheKey);
            Interlocked.Increment(ref _totalMisses);
            return null;
        }
    }

    public async Task SetCachedToolsAsync(
        List<int> functionConfigurationIds,
        List<Tool> tools,
        int? ttlMinutes = null,
        CancellationToken cancellationToken = default)
    {
        // Check global enable/disable setting first
        if (!await IsCachingEnabledAsync(cancellationToken))
        {
            _logger.LogDebug("Function discovery caching is globally disabled. Skipping cache write.");
            return;
        }

        if (functionConfigurationIds == null || functionConfigurationIds.Count == 0)
        {
            return;
        }

        try
        {
            // Determine TTL: use override, or calculate minimum TTL from all function configs
            TimeSpan expiration;
            if (ttlMinutes.HasValue)
            {
                expiration = TimeSpan.FromMinutes(ttlMinutes.Value);
            }
            else
            {
                // Get all function configurations to check their TTL settings
                var configs = await _functionConfigRepository.GetByIdsAsync(functionConfigurationIds, cancellationToken);

                // Find minimum TTL among all configs that have TTL set
                var configsWithTtl = configs.Where(c => c.CacheTtlMinutes.HasValue).ToList();

                if (!configsWithTtl.Any())
                {
                    // None of the functions have TTL configured - don't cache
                    _logger.LogDebug("No function configurations have CacheTtlMinutes set. Skipping cache write for {Count} configs.",
                        functionConfigurationIds.Count);
                    return;
                }

                // Use minimum TTL so cache expires when the shortest-lived function config expires
                var minTtl = configsWithTtl.Min(c => c.CacheTtlMinutes!.Value);
                expiration = TimeSpan.FromMinutes(minTtl);

                _logger.LogDebug("Using minimum TTL of {MinTtl} minutes from {ConfigCount} function configs",
                    minTtl, configsWithTtl.Count);
            }

            var cacheKey = BuildCacheKey(functionConfigurationIds);

            await _cacheManager.SetAsync(cacheKey, tools, FUNCTION_DISCOVERY_REGION, expiration, cancellationToken);

            _logger.LogInformation(
                "Cached {ToolCount} tool definitions for {ConfigCount} function configs. Key: {CacheKey}, TTL: {Minutes} minutes",
                tools.Count, functionConfigurationIds.Count, cacheKey, expiration.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting function discovery results in cache");
        }
    }

    public async Task InvalidateAllFunctionDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _lastInvalidation = DateTime.UtcNow;

            // Use CacheManager's ClearRegionAsync for surgical invalidation
            // This uses the tracked keys to remove only function discovery entries from both memory and Redis
            await _cacheManager.ClearRegionAsync(FUNCTION_DISCOVERY_REGION, cancellationToken);

            _logger.LogInformation("Invalidated all function discovery cache entries at {Time} using CacheManager.ClearRegionAsync",
                _lastInvalidation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating all function discovery cache entries");
        }
    }

    public async Task InvalidateFunctionConfigurationAsync(
        int functionConfigurationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _lastInvalidation = DateTime.UtcNow;

            // For simplicity, invalidate all function discovery cache entries
            // since we cache by lists of IDs, not individual IDs
            // More sophisticated approach would be to track which cache entries contain this function
            await InvalidateAllFunctionDiscoveryAsync(cancellationToken);

            _logger.LogInformation(
                "Invalidated function discovery cache for function configuration {FunctionConfigId} (all entries cleared)",
                functionConfigurationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating function discovery cache for function configuration {FunctionConfigId}",
                functionConfigurationId);
        }
    }

    public async Task<FunctionDiscoveryCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var hits = Interlocked.Read(ref _totalHits);
        var misses = Interlocked.Read(ref _totalMisses);
        var total = hits + misses;
        var isEnabled = await IsCachingEnabledAsync(cancellationToken);

        var stats = new FunctionDiscoveryCacheStatistics
        {
            Hits = hits,
            Misses = misses,
            HitRate = total > 0 ? (double)hits / total * 100 : 0,
            CachedEntries = 0, // Would require cache key scanning in production
            LastInvalidation = _lastInvalidation,
            IsEnabled = isEnabled
        };

        return stats;
    }

    /// <summary>
    /// Builds cache key for function discovery results.
    /// Note: CacheManager handles region prefixing internally, so we only need the logical key.
    /// </summary>
    private static string BuildCacheKey(List<int> functionConfigurationIds)
    {
        // Sort IDs to ensure consistent cache keys regardless of order
        var sortedIds = functionConfigurationIds.OrderBy(id => id).ToList();
        return $"configs:{string.Join(",", sortedIds)}";
    }
}
