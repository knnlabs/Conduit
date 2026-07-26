using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Service for managing and retrieving function costs, with hybrid caching support (L1: Memory, L2: Redis).
/// </summary>
/// <remarks>
/// This service follows the same architecture as ModelCostService for LLM operations.
/// It provides cached access to function cost configurations and handles priority-based selection.
///
/// Caching Strategy:
/// - L1 Cache: In-process Memory (15 minute TTL)
/// - L2 Cache: Redis Distributed (1 hour TTL)
/// - Cascade: L1 → L2 → Database
/// </remarks>
public class FunctionCostService : IFunctionCostService
{
    private readonly IFunctionCostRepository _functionCostRepository;
    private readonly IFunctionCostMappingRepository _functionCostMappingRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<FunctionCostService> _logger;
    private readonly TimeSpan _memoryCacheDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _distributedCacheDuration = TimeSpan.FromHours(1);
    private readonly JsonSerializerOptions _jsonOptions;
    private const string CacheKeyPrefix = "FunctionCost_";
    private const string AllCostsCacheKey = CacheKeyPrefix + "All";

    /// <summary>
    /// Creates a new instance of the FunctionCostService.
    /// </summary>
    /// <param name="functionCostRepository">The function cost repository.</param>
    /// <param name="functionCostMappingRepository">The function cost mapping repository.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="distributedCache">The distributed cache (optional, Redis).</param>
    public FunctionCostService(
        IFunctionCostRepository functionCostRepository,
        IFunctionCostMappingRepository functionCostMappingRepository,
        IMemoryCache memoryCache,
        ILogger<FunctionCostService> logger,
        IDistributedCache? distributedCache = null)
    {
        _functionCostRepository = functionCostRepository ?? throw new ArgumentNullException(nameof(functionCostRepository));
        _functionCostMappingRepository = functionCostMappingRepository ?? throw new ArgumentNullException(nameof(functionCostMappingRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _distributedCache = distributedCache;
        _jsonOptions = Utilities.FunctionsJsonOptions.Compact;
    }

    /// <inheritdoc />
    public async Task<FunctionCost?> GetCostForConfigurationAsync(
        int functionConfigurationId,
        CancellationToken cancellationToken = default)
    {
        if (functionConfigurationId <= 0)
        {
            throw new ArgumentException("Function configuration ID must be greater than zero", nameof(functionConfigurationId));
        }

        try
        {
            string cacheKey = $"{CacheKeyPrefix}Config_{functionConfigurationId}";

            // Try hybrid cache first
            var cachedCost = await GetFromHybridCacheAsync<FunctionCost?>(cacheKey);
            if (cachedCost != null)
            {
                _logger.LogDebug("Cache hit for function cost: ConfigId={ConfigId}", functionConfigurationId);
                return cachedCost;
            }

            _logger.LogDebug("Cache miss for function cost: ConfigId={ConfigId}, querying database", functionConfigurationId);

            // Get active cost mappings for this configuration
            var mappings = await _functionCostMappingRepository.GetByFunctionConfigurationIdAsync(functionConfigurationId, cancellationToken);

            if (mappings == null || !mappings.Any())
            {
                _logger.LogDebug("No cost mappings found for function configuration: {ConfigId}", functionConfigurationId);
                return null;
            }

            // Load full cost objects for active mappings
            var costIds = mappings.Where(m => m.IsActive).Select(m => m.FunctionCostId).Distinct();
            var costs = new List<FunctionCost>();

            foreach (var costId in costIds)
            {
                var cost = await _functionCostRepository.GetByIdAsync(costId, cancellationToken);
                if (cost != null)
                {
                    costs.Add(cost);
                }
            }

            // Filter by active status and effective date range, then select highest priority
            var now = DateTime.UtcNow;
            var functionCost = costs
                .Where(cost => cost.IsActive && cost.EffectiveDate <= now)
                .Where(cost => !cost.ExpiryDate.HasValue || cost.ExpiryDate.Value > now)
                .OrderByDescending(cost => cost.Priority)
                .ThenByDescending(cost => cost.EffectiveDate)
                .FirstOrDefault();

            if (functionCost == null)
            {
                _logger.LogDebug("No active function cost found for configuration: {ConfigId}", functionConfigurationId);
            }
            else
            {
                _logger.LogDebug("Found function cost: {CostName} (Priority={Priority}) for configuration: {ConfigId}",
                    functionCost.CostName, functionCost.Priority, functionConfigurationId);
            }

            await SetInHybridCacheAsync(cacheKey, functionCost);
            return functionCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost for function configuration {ConfigId}", functionConfigurationId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FunctionCost?> GetCostByIdAsync(int costId, CancellationToken cancellationToken = default)
    {
        if (costId <= 0)
        {
            throw new ArgumentException("Cost ID must be greater than zero", nameof(costId));
        }

        try
        {
            string cacheKey = $"{CacheKeyPrefix}Id_{costId}";

            // Try hybrid cache first
            var cachedCost = await GetFromHybridCacheAsync<FunctionCost?>(cacheKey);
            if (cachedCost != null)
            {
                _logger.LogDebug("Cache hit for function cost ID: {CostId}", costId);
                return cachedCost;
            }

            var cost = await _functionCostRepository.GetByIdAsync(costId, cancellationToken);

            if (cost != null)
            {
                await SetInHybridCacheAsync(cacheKey, cost);
            }

            return cost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function cost by ID: {CostId}", costId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<FunctionCost>> ListCostsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        try
        {
            string cacheKey = activeOnly ? $"{AllCostsCacheKey}_Active" : AllCostsCacheKey;

            // Try hybrid cache first
            var cachedCosts = await GetFromHybridCacheAsync<List<FunctionCost>?>(cacheKey);
            if (cachedCosts != null)
            {
                _logger.LogDebug("Cache hit for function costs list (activeOnly={ActiveOnly})", activeOnly);
                return cachedCosts;
            }

            var costs = await _functionCostRepository.GetAllAsync(cancellationToken);

            if (activeOnly)
            {
                var now = DateTime.UtcNow;
                costs = costs
                    .Where(c => c.IsActive && c.EffectiveDate <= now)
                    .Where(c => !c.ExpiryDate.HasValue || c.ExpiryDate.Value > now)
                    .ToList();
            }

            await SetInHybridCacheAsync(cacheKey, costs);
            return costs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing function costs");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CreateCostAsync(FunctionCost cost, CancellationToken cancellationToken = default)
    {
        if (cost == null)
        {
            throw new ArgumentNullException(nameof(cost));
        }

        try
        {
            cost.CreatedAt = DateTime.UtcNow;
            cost.UpdatedAt = DateTime.UtcNow;

            var costId = await _functionCostRepository.CreateAsync(cost, cancellationToken);

            await InvalidateCacheAsync(costId, []);

            _logger.LogInformation("Created function cost: {CostName} (ID={CostId})", cost.CostName, costId);
            return costId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function cost: {CostName}", cost.CostName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateCostAsync(FunctionCost cost, CancellationToken cancellationToken = default)
    {
        if (cost == null)
        {
            throw new ArgumentNullException(nameof(cost));
        }

        try
        {
            var affectedConfigurationIds = await GetMappedConfigurationIdsAsync(cost.Id, cancellationToken);
            cost.UpdatedAt = DateTime.UtcNow;

            await _functionCostRepository.UpdateAsync(cost, cancellationToken);

            await InvalidateCacheAsync(cost.Id, affectedConfigurationIds);

            _logger.LogInformation("Updated function cost: {CostName} (ID={CostId})", cost.CostName, cost.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function cost: {CostId}", cost.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCostAsync(int costId, CancellationToken cancellationToken = default)
    {
        if (costId <= 0)
        {
            throw new ArgumentException("Cost ID must be greater than zero", nameof(costId));
        }

        try
        {
            var affectedConfigurationIds = await GetMappedConfigurationIdsAsync(costId, cancellationToken);
            await _functionCostRepository.DeleteAsync(costId, cancellationToken);

            await InvalidateCacheAsync(costId, affectedConfigurationIds);

            _logger.LogInformation("Deleted function cost: ID={CostId}", costId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function cost: {CostId}", costId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync()
    {
        await InvalidateCacheAsync(null, []);
    }

    private async Task<List<int>> GetMappedConfigurationIdsAsync(
        int costId,
        CancellationToken cancellationToken)
    {
        var existingCost = await _functionCostRepository.GetByIdAsync(costId, cancellationToken);
        return existingCost?.FunctionMappings
            .Select(mapping => mapping.FunctionConfigurationId)
            .Distinct()
            .ToList() ?? [];
    }

    private async Task InvalidateCacheAsync(int? costId, IEnumerable<int> functionConfigurationIds)
    {
        try
        {
            var keysToRemove = new HashSet<string>
            {
                AllCostsCacheKey,
                $"{AllCostsCacheKey}_Active"
            };

            if (costId.HasValue)
            {
                keysToRemove.Add($"{CacheKeyPrefix}Id_{costId.Value}");
            }

            foreach (var functionConfigurationId in functionConfigurationIds)
            {
                keysToRemove.Add($"{CacheKeyPrefix}Config_{functionConfigurationId}");
            }

            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
            }

            // Clear distributed cache if available
            if (_distributedCache != null)
            {
                foreach (var key in keysToRemove)
                {
                    await _distributedCache.RemoveAsync(key);
                }
            }

            _logger.LogInformation(
                "Cleared {CacheKeyCount} function cost cache entries for CostId={CostId}",
                keysToRemove.Count,
                costId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing function cost cache");
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets an item from the hybrid cache (L1 memory, L2 distributed).
    /// </summary>
    private async Task<T?> GetFromHybridCacheAsync<T>(string key) where T : class?
    {
        // Try L1 cache (memory) first
        if (_memoryCache.TryGetValue(key, out T? cachedValue))
        {
            return cachedValue;
        }

        // Try L2 cache (distributed/Redis) if available
        if (_distributedCache != null)
        {
            try
            {
                var distributedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(distributedValue))
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue, _jsonOptions);

                    // Populate L1 cache
                    if (deserializedValue != null)
                    {
                        _memoryCache.Set(key, deserializedValue, _memoryCacheDuration);
                    }

                    return deserializedValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading from distributed cache for key: {Key}", key);
                // Fall through to return null
            }
        }

        return null;
    }

    /// <summary>
    /// Sets an item in the hybrid cache (L1 memory, L2 distributed).
    /// </summary>
    private async Task SetInHybridCacheAsync<T>(string key, T? value) where T : class?
    {
        if (value == null)
        {
            return;
        }

        // Set in L1 cache (memory)
        _memoryCache.Set(key, value, _memoryCacheDuration);

        // Set in L2 cache (distributed/Redis) if available
        if (_distributedCache != null)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _distributedCacheDuration
                };
                await _distributedCache.SetStringAsync(key, serializedValue, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error writing to distributed cache for key: {Key}", key);
                // Don't fail the operation if distributed cache write fails
            }
        }
    }

    #endregion
}
