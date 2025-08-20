using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Model Cost cache - Helper methods and event handlers
    /// </summary>
    public partial class RedisModelCostCache
    {
        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<ModelCostCacheStats> GetStatsAsync()
        {
            try
            {
                var hits = await _database.StringGetAsync(STATS_HIT_KEY);
                var misses = await _database.StringGetAsync(STATS_MISS_KEY);
                var invalidations = await _database.StringGetAsync(STATS_INVALIDATION_KEY);
                var patternMatches = await _database.StringGetAsync(STATS_PATTERN_MATCH_KEY);
                var resetTime = await _database.StringGetAsync(STATS_RESET_TIME_KEY);
                
                // Count entries
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: KeyPrefix + "*");
                var entryCount = 0L;
                foreach (var _ in keys)
                {
                    entryCount++;
                }
                
                return new ModelCostCacheStats
                {
                    HitCount = hits.HasValue ? (long)hits : 0,
                    MissCount = misses.HasValue ? (long)misses : 0,
                    InvalidationCount = invalidations.HasValue ? (long)invalidations : 0,
                    PatternMatchCount = patternMatches.HasValue ? (long)patternMatches : 0,
                    LastResetTime = resetTime.HasValue && DateTime.TryParse(resetTime, out var time) ? time : DateTime.UtcNow,
                    EntryCount = entryCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost cache statistics");
                return new ModelCostCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task SetModelCostAsync(ModelCost cost)
        {
            var patternKey = PatternKeyPrefix + cost.CostName.ToLowerInvariant();
            
            // Create cached version with pre-parsed configuration
            var cachedCost = ConvertToCachedModelCost(cost);
            var serialized = JsonSerializer.Serialize(cachedCost, _jsonOptions);
            
            await _database.StringSetAsync(patternKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Model cost cached for cost name: {CostName}", cost.CostName);
        }

        /*
        private async Task SetProviderModelCostsAsync(string providerName, List<ModelCost> costs)
        {
            var providerKey = ProviderKeyPrefix + providerName.ToLowerInvariant();
            var serialized = JsonSerializer.Serialize(costs, _jsonOptions);
            
            await _database.StringSetAsync(providerKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Model costs cached for provider: {Provider} ({Count} costs)", providerName, costs.Count());
        }
        */

        /// <summary>
        /// Handle single invalidation messages from other instances
        /// </summary>
        private async void OnCostInvalidated(RedisChannel channel, RedisValue costId)
        {
            try
            {
                if (int.TryParse(costId, out var id))
                {
                    await InvalidateModelCostAsync(id);
                    _logger.LogDebug("Invalidated model cost from pub/sub: {CostId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling cost invalidation: {CostId}", costId.ToString());
            }
        }

        /// <summary>
        /// Handle batch invalidation messages from other instances
        /// </summary>
        private async void OnBatchInvalidated(RedisChannel channel, RedisValue message)
        {
            try
            {
                var batchMessage = JsonSerializer.Deserialize<ModelCostBatchInvalidation>(message!);
                if (batchMessage?.CostIds != null)
                {
                    var requests = batchMessage.CostIds.Select(id => new InvalidationRequest
                    {
                        EntityType = CacheType.ModelCost.ToString(),
                        EntityId = id,
                        Reason = "Batch invalidation from pub/sub"
                    });
                    
                    await InvalidateBatchAsync(requests);
                    
                    _logger.LogDebug(
                        "Batch invalidated {Count} model costs from pub/sub",
                        batchMessage.CostIds.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling batch cost invalidation");
            }
        }

        /// <summary>
        /// Converts a ModelCost entity to a CachedModelCost with pre-parsed configuration.
        /// </summary>
        private CachedModelCost ConvertToCachedModelCost(ModelCost cost)
        {
            var cached = new CachedModelCost
            {
                Id = cost.Id,
                CostName = cost.CostName,
                PricingModel = cost.PricingModel,
                InputCostPerMillionTokens = cost.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = cost.OutputCostPerMillionTokens,
                EmbeddingCostPerMillionTokens = cost.EmbeddingCostPerMillionTokens,
                ImageCostPerImage = cost.ImageCostPerImage,
                VideoCostPerSecond = cost.VideoCostPerSecond,
                BatchProcessingMultiplier = cost.BatchProcessingMultiplier,
                SupportsBatchProcessing = cost.SupportsBatchProcessing,
                CachedInputCostPerMillionTokens = cost.CachedInputCostPerMillionTokens,
                CachedInputWriteCostPerMillionTokens = cost.CachedInputWriteCostPerMillionTokens,
                CostPerSearchUnit = cost.CostPerSearchUnit,
                CostPerInferenceStep = cost.CostPerInferenceStep,
                DefaultInferenceSteps = cost.DefaultInferenceSteps,
                AudioCostPerMinute = cost.AudioCostPerMinute,
                AudioCostPerKCharacters = cost.AudioCostPerKCharacters,
                AudioInputCostPerMinute = cost.AudioInputCostPerMinute,
                AudioOutputCostPerMinute = cost.AudioOutputCostPerMinute,
                ModelType = cost.ModelType,
                IsActive = cost.IsActive,
                Priority = cost.Priority,
                Description = cost.Description
            };

            // Parse JSON multipliers
            if (!string.IsNullOrEmpty(cost.VideoResolutionMultipliers))
            {
                try
                {
                    cached.VideoResolutionMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(cost.VideoResolutionMultipliers);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse VideoResolutionMultipliers for cost {CostName}", cost.CostName);
                }
            }

            if (!string.IsNullOrEmpty(cost.ImageQualityMultipliers))
            {
                try
                {
                    cached.ImageQualityMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(cost.ImageQualityMultipliers);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ImageQualityMultipliers for cost {CostName}", cost.CostName);
                }
            }

            if (!string.IsNullOrEmpty(cost.ImageResolutionMultipliers))
            {
                try
                {
                    cached.ImageResolutionMultipliers = JsonSerializer.Deserialize<Dictionary<string, decimal>>(cost.ImageResolutionMultipliers);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ImageResolutionMultipliers for cost {CostName}", cost.CostName);
                }
            }

            // Parse pricing configuration based on pricing model
            if (!string.IsNullOrEmpty(cost.PricingConfiguration))
            {
                try
                {
                    cached.ParsedPricingConfiguration = cost.PricingModel switch
                    {
                        PricingModel.PerVideo => JsonSerializer.Deserialize<PerVideoPricingConfig>(cost.PricingConfiguration, _jsonOptions),
                        PricingModel.PerSecondVideo => JsonSerializer.Deserialize<PerSecondVideoPricingConfig>(cost.PricingConfiguration, _jsonOptions),
                        PricingModel.InferenceSteps => JsonSerializer.Deserialize<InferenceStepsPricingConfig>(cost.PricingConfiguration, _jsonOptions),
                        PricingModel.TieredTokens => JsonSerializer.Deserialize<TieredTokensPricingConfig>(cost.PricingConfiguration, _jsonOptions),
                        PricingModel.PerImage => JsonSerializer.Deserialize<PerImagePricingConfig>(cost.PricingConfiguration, _jsonOptions),
                        _ => null
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse PricingConfiguration for cost {CostName} with model {PricingModel}", 
                        cost.CostName, cost.PricingModel);
                }
            }

            return cached;
        }
    }
}