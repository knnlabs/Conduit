using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for managing and retrieving model costs, with caching support
/// </summary>
public class ModelCostService : IModelCostService
{
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ModelCostService> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);
    private const string CacheKeyPrefix = "ModelCost_";
    private const string AllModelsCacheKey = CacheKeyPrefix + "All";

    public ModelCostService(
        IDbContextFactory<ConfigurationDbContext> dbContextFactory,
        IMemoryCache cache,
        ILogger<ModelCostService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModelCost?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID cannot be empty", nameof(modelId));
        }

        string cacheKey = $"{CacheKeyPrefix}{modelId}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out ModelCost? cachedCost))
        {
            _logger.LogDebug("Cache hit for model cost: {ModelId}", modelId);
            return cachedCost;
        }

        _logger.LogDebug("Cache miss for model cost: {ModelId}, querying database", modelId);

        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // First, try to find an exact match
        var exactMatch = await dbContext.ModelCosts
            .FirstOrDefaultAsync(c => c.ModelIdPattern == modelId, cancellationToken);

        if (exactMatch != null)
        {
            _cache.Set(cacheKey, exactMatch, _cacheDuration);
            return exactMatch;
        }

        // If no exact match, look for wildcard patterns
        var wildcardPatterns = await dbContext.ModelCosts
            .Where(c => c.ModelIdPattern.EndsWith("*"))
            .ToListAsync(cancellationToken);

        if (!wildcardPatterns.Any())
        {
            _cache.Set<ModelCost?>(cacheKey, null, _cacheDuration);
            return null;
        }

        // Find the best matching pattern (longest prefix match)
        ModelCost? bestMatch = null;
        int longestMatchLength = 0;

        foreach (var pattern in wildcardPatterns)
        {
            // Remove the trailing asterisk for comparison
            string patternPrefix = pattern.ModelIdPattern.TrimEnd('*');
            
            if (modelId.StartsWith(patternPrefix) && patternPrefix.Length > longestMatchLength)
            {
                bestMatch = pattern;
                longestMatchLength = patternPrefix.Length;
            }
        }

        _cache.Set<ModelCost?>(cacheKey, bestMatch, _cacheDuration);
        return bestMatch;
    }

    /// <inheritdoc />
    public async Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_cache.TryGetValue(AllModelsCacheKey, out List<ModelCost>? cachedCosts) && cachedCosts != null)
        {
            return cachedCosts;
        }

        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var costs = await dbContext.ModelCosts.ToListAsync(cancellationToken);
        
        _cache.Set(AllModelsCacheKey, costs, _cacheDuration);
        return costs;
    }

    /// <inheritdoc />
    public async Task AddModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
    {
        if (modelCost == null)
        {
            throw new ArgumentNullException(nameof(modelCost));
        }

        modelCost.CreatedAt = DateTime.UtcNow;
        modelCost.UpdatedAt = DateTime.UtcNow;

        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ModelCosts.AddAsync(modelCost, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Clear cache
        ClearCache();
    }

    /// <inheritdoc />
    public async Task<bool> UpdateModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
    {
        if (modelCost == null)
        {
            throw new ArgumentNullException(nameof(modelCost));
        }

        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingCost = await dbContext.ModelCosts.FindAsync(new object[] { modelCost.Id }, cancellationToken);

        if (existingCost == null)
        {
            return false;
        }

        // Update properties
        existingCost.ModelIdPattern = modelCost.ModelIdPattern;
        existingCost.InputTokenCost = modelCost.InputTokenCost;
        existingCost.OutputTokenCost = modelCost.OutputTokenCost;
        existingCost.EmbeddingTokenCost = modelCost.EmbeddingTokenCost;
        existingCost.ImageCostPerImage = modelCost.ImageCostPerImage;
        existingCost.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Clear cache
        ClearCache();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteModelCostAsync(int id, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var modelCost = await dbContext.ModelCosts.FindAsync(new object[] { id }, cancellationToken);

        if (modelCost == null)
        {
            return false;
        }

        dbContext.ModelCosts.Remove(modelCost);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Clear cache
        ClearCache();
        return true;
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
