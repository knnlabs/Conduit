using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Caching decorator for IModelCostService that reduces database load
    /// by caching model cost lookups via the CacheManager.
    /// </summary>
    /// <remarks>
    /// This decorator implements the Decorator pattern to add caching capabilities
    /// to the existing ModelCostService without modifying its core logic.
    ///
    /// Caching Strategy:
    /// - Uses CacheRegion.ModelCosts for model cost data
    /// - TTL: 15 minutes (region default)
    /// - Read methods use Get + Set (not GetOrCreate) because results can be null
    /// - Write methods delegate to inner service then clear the entire region
    /// - Graceful fallback to inner service on cache errors
    /// </remarks>
    public class CachedModelCostService : IModelCostService
    {
        private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(1);
        private const string MissingMarker = "missing";

        private readonly IModelCostService _innerService;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<CachedModelCostService> _logger;

        private const CacheRegion Region = CacheRegion.ModelCosts;

        public CachedModelCostService(
            IModelCostService innerService,
            ICacheManager cacheManager,
            ILogger<CachedModelCostService> logger)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ModelCost?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be empty", nameof(modelId));
            }

            var cacheKey = CacheKeys.ModelCost.ByModelId(modelId);

            if (await IsNegativeCacheHitAsync(cacheKey, cancellationToken))
            {
                _logger.LogWarning("Negative cache hit for missing model cost: {ModelId}", modelId);
                return null;
            }

            try
            {
                var cached = await _cacheManager.GetAsync<ModelCost>(cacheKey, Region, cancellationToken);
                if (cached != null && IsCurrentlyActive(cached))
                {
                    _logger.LogDebug("Cache hit for model cost: {ModelId}", modelId);
                    return cached;
                }

                if (cached != null)
                {
                    _logger.LogWarning(
                        "Cached model cost {ModelCostId} for {ModelId} became inactive or expired; refreshing from database",
                        cached.Id, modelId);
                    await _cacheManager.RemoveAsync(cacheKey, Region, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache read failed for model cost {ModelId}, falling back to database", modelId);
            }

            var result = await _innerService.GetCostForModelAsync(modelId, cancellationToken);

            if (result != null)
            {
                try
                {
                    await _cacheManager.SetAsync(cacheKey, result, Region, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cache write failed for model cost {ModelId}", modelId);
                }
            }
            else
            {
                await SetNegativeCacheAsync(cacheKey, cancellationToken);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ModelCost?> GetCostByIdAsync(int modelCostId, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKeys.ModelCost.ById(modelCostId);

            if (await IsNegativeCacheHitAsync(cacheKey, cancellationToken))
            {
                _logger.LogWarning("Negative cache hit for missing model cost ID: {ModelCostId}", modelCostId);
                return null;
            }

            try
            {
                var cached = await _cacheManager.GetAsync<ModelCost>(cacheKey, Region, cancellationToken);
                if (cached != null && IsCurrentlyActive(cached))
                {
                    _logger.LogDebug("Cache hit for model cost ID: {ModelCostId}", modelCostId);
                    return cached;
                }

                if (cached != null)
                {
                    _logger.LogWarning(
                        "Cached model cost ID {ModelCostId} became inactive or expired; refreshing from database",
                        modelCostId);
                    await _cacheManager.RemoveAsync(cacheKey, Region, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache read failed for model cost ID {ModelCostId}, falling back to database", modelCostId);
            }

            var result = await _innerService.GetCostByIdAsync(modelCostId, cancellationToken);

            if (result != null)
            {
                try
                {
                    await _cacheManager.SetAsync(cacheKey, result, Region, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cache write failed for model cost ID {ModelCostId}", modelCostId);
                }
            }
            else
            {
                await SetNegativeCacheAsync(cacheKey, cancellationToken);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var cached = await _cacheManager.GetAsync<List<ModelCost>>(CacheKeys.ModelCost.All, Region, cancellationToken);
                if (cached != null)
                {
                    return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache read failed for all model costs, falling back to database");
            }

            var result = await _innerService.ListModelCostsAsync(cancellationToken);

            try
            {
                await _cacheManager.SetAsync(CacheKeys.ModelCost.All, result, Region, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache write failed for all model costs");
            }

            return result;
        }

        /// <inheritdoc />
        public async Task AddModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            await _innerService.AddModelCostAsync(modelCost, cancellationToken);
            await InvalidateAllAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            var result = await _innerService.UpdateModelCostAsync(modelCost, cancellationToken);
            if (result)
            {
                await InvalidateAllAsync(cancellationToken);
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelCostAsync(int id, CancellationToken cancellationToken = default)
        {
            var result = await _innerService.DeleteModelCostAsync(id, cancellationToken);
            if (result)
            {
                await InvalidateAllAsync(cancellationToken);
            }
            return result;
        }

        /// <inheritdoc />
        public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            await InvalidateAllAsync(cancellationToken);
        }

        private async Task InvalidateAllAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _cacheManager.ClearRegionAsync(Region, cancellationToken);
                _logger.LogInformation("Model cost cache region cleared");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing model cost cache region");
            }
        }

        private async Task<bool> IsNegativeCacheHitAsync(string cacheKey, CancellationToken cancellationToken)
        {
            try
            {
                var marker = await _cacheManager.GetAsync<string>(
                    $"{cacheKey}:negative", Region, cancellationToken);
                return marker == MissingMarker;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Negative cache read failed for model cost key {CacheKey}", cacheKey);
                return false;
            }
        }

        private async Task SetNegativeCacheAsync(string cacheKey, CancellationToken cancellationToken)
        {
            try
            {
                await _cacheManager.SetAsync(
                    $"{cacheKey}:negative", MissingMarker, Region, NegativeCacheTtl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Negative cache write failed for model cost key {CacheKey}", cacheKey);
            }
        }

        private static bool IsCurrentlyActive(ModelCost modelCost)
        {
            var now = DateTime.UtcNow;
            return modelCost.IsActive &&
                modelCost.EffectiveDate <= now &&
                (!modelCost.ExpiryDate.HasValue || modelCost.ExpiryDate.Value > now);
        }
    }
}
