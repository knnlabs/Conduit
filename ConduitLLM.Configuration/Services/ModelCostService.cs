using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for managing and retrieving model costs, with caching support
/// </summary>
public class ModelCostService : IModelCostService
{
    private readonly IModelCostRepository _modelCostRepository;
    private readonly IModelProviderMappingRepository _modelProviderMappingRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ModelCostService> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);
    private const string CacheKeyPrefix = "ModelCost_";
    private const string AllModelsCacheKey = CacheKeyPrefix + "All";

    /// <summary>
    /// Creates a new instance of the ModelCostService
    /// </summary>
    /// <param name="modelCostRepository">The model cost repository</param>
    /// <param name="modelProviderMappingRepository">The model provider mapping repository</param>
    /// <param name="cache">The memory cache</param>
    /// <param name="logger">The logger</param>
    public ModelCostService(
        IModelCostRepository modelCostRepository,
        IModelProviderMappingRepository modelProviderMappingRepository,
        IMemoryCache cache,
        ILogger<ModelCostService> logger)
    {
        _modelCostRepository = modelCostRepository ?? throw new ArgumentNullException(nameof(modelCostRepository));
        _modelProviderMappingRepository = modelProviderMappingRepository ?? throw new ArgumentNullException(nameof(modelProviderMappingRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out ModelCost? cachedCost))
            {
                _logger.LogDebug("Cache hit for model cost: {ModelId}", modelId);
                return cachedCost;
            }

            _logger.LogDebug("Cache miss for model cost: {ModelId}, querying database", modelId);

            // Find the ModelProviderMapping by alias
            var modelMapping = await _modelProviderMappingRepository.GetByModelNameAsync(modelId, cancellationToken);

            if (modelMapping == null)
            {
                _logger.LogDebug("No model provider mapping found for alias: {ModelId}", modelId);
                _cache.Set<ModelCost?>(cacheKey, null, _cacheDuration);
                return null;
            }

            // Get the associated cost through the junction table
            // First, get all model costs to include the navigation properties
            var allCosts = await _modelCostRepository.GetAllAsync(cancellationToken);
            
            // Find the cost associated with this model mapping
            var modelCost = allCosts.FirstOrDefault(cost => 
                cost.ModelCostMappings.Any(mapping => 
                    mapping.ModelProviderMappingId == modelMapping.Id && 
                    mapping.IsActive));

            // If we found multiple costs, prioritize by:
            // 1. Active status
            // 2. Effective date (most recent that's not in the future)
            // 3. Priority
            if (modelCost == null)
            {
                var now = DateTime.UtcNow;
                var candidateCosts = allCosts
                    .Where(cost => cost.ModelCostMappings.Any(mapping => 
                        mapping.ModelProviderMappingId == modelMapping.Id &&
                        mapping.IsActive))
                    .Where(cost => cost.IsActive && cost.EffectiveDate <= now)
                    .Where(cost => !cost.ExpiryDate.HasValue || cost.ExpiryDate.Value > now)
                    .OrderByDescending(cost => cost.Priority)
                    .ThenByDescending(cost => cost.EffectiveDate);

                modelCost = candidateCosts.FirstOrDefault();
            }

            _cache.Set<ModelCost?>(cacheKey, modelCost, _cacheDuration);
            return modelCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost for model {ModelId}", modelId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get from cache first
            if (_cache.TryGetValue(AllModelsCacheKey, out List<ModelCost>? cachedCosts) && cachedCosts != null)
            {
                return cachedCosts;
            }

            var costs = await _modelCostRepository.GetAllAsync(cancellationToken);

            _cache.Set(AllModelsCacheKey, costs, _cacheDuration);
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
            ClearCache();
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
            existingCost.InputTokenCost = modelCost.InputTokenCost;
            existingCost.OutputTokenCost = modelCost.OutputTokenCost;
            existingCost.EmbeddingTokenCost = modelCost.EmbeddingTokenCost;
            existingCost.ImageCostPerImage = modelCost.ImageCostPerImage;
            existingCost.UpdatedAt = DateTime.UtcNow;

            bool result = await _modelCostRepository.UpdateAsync(existingCost, cancellationToken);

            // Clear cache
            ClearCache();
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
                ClearCache();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model cost with ID {ModelCostId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        // Remove all ModelCost-related entries from the cache
        // This is a simple approach; in a production environment with many entries,
        // you might want to use a more sophisticated cache invalidation strategy
        _logger.LogInformation("Clearing model cost cache");

        // First, remove the 'all models' cache
        _cache.Remove(AllModelsCacheKey);

        // For a distributed cache system, you might need a more advanced approach
        // to track and remove all cache keys, potentially using a separate list of keys
    }
}
