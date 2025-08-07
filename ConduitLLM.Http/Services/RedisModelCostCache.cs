using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
    /// Redis-based Model Cost cache with event-driven invalidation
    /// </summary>
    public class RedisModelCostCache : IModelCostCache, IBatchInvalidatable
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisModelCostCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(6); // Model costs change infrequently
        private const string KeyPrefix = "modelcost:";
        private const string PatternKeyPrefix = "modelcost:pattern:";
        private const string ProviderKeyPrefix = "modelcost:provider:";
        
        // Statistics tracking keys
        private const string STATS_HIT_KEY = "conduit:cache:modelcost:stats:hits";
        private const string STATS_MISS_KEY = "conduit:cache:modelcost:stats:misses";
        private const string STATS_INVALIDATION_KEY = "conduit:cache:modelcost:stats:invalidations";
        private const string STATS_RESET_TIME_KEY = "conduit:cache:modelcost:stats:reset_time";
        private const string STATS_PATTERN_MATCH_KEY = "conduit:cache:modelcost:stats:pattern_matches";
        
        private const string InvalidationChannel = "mcost_invalidated";
        private const string BatchInvalidationChannel = "mcost_batch_invalidated";
        private readonly ISubscriber _subscriber;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisModelCostCache(
            IConnectionMultiplexer redis,
            ILogger<RedisModelCostCache> logger)
        {
            _database = redis.GetDatabase();
            _subscriber = redis.GetSubscriber();
            _logger = logger;
            
            // Initialize stats reset time if not exists
            _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.ToString("O"), when: When.NotExists).GetAwaiter().GetResult();
            
            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(InvalidationChannel), OnCostInvalidated);
            _subscriber.Subscribe(RedisChannel.Literal(BatchInvalidationChannel), OnBatchInvalidated);
        }

        /// <summary>
        /// Get Model Cost by pattern from cache with database fallback
        /// </summary>
        public async Task<ModelCost?> GetModelCostByPatternAsync(
            string modelIdPattern, 
            Func<string, Task<ModelCost?>> databaseFallback)
        {
            var cacheKey = PatternKeyPrefix + modelIdPattern.ToLowerInvariant();
            
            try
            {
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var cost = JsonSerializer.Deserialize<ModelCost>(jsonString, _jsonOptions);
                        
                        if (cost != null)
                        {
                            _logger.LogDebug("Model cost cache hit for pattern: {Pattern}", modelIdPattern);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return cost;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Model cost cache miss for pattern, querying database: {Pattern}", modelIdPattern);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbCost = await databaseFallback(modelIdPattern);
                
                if (dbCost != null)
                {
                    // Cache the cost
                    await SetModelCostAsync(dbCost);
                    return dbCost;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Model Cost cache for pattern, falling back to database: {Pattern}", modelIdPattern);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback(modelIdPattern);
            }
        }

        /*
        /// <summary>
        /// Get all Model Costs for a provider from cache with database fallback
        /// NOTE: This method is disabled as ModelCost entity doesn't contain provider information
        /// </summary>
        public async Task<List<ModelCost>> GetProviderModelCostsAsync(
            string providerName, 
            Func<string, Task<List<ModelCost>>> databaseFallback)
        {
            var cacheKey = ProviderKeyPrefix + providerName.ToLowerInvariant();
            
            try
            {
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var costs = JsonSerializer.Deserialize<List<ModelCost>>(jsonString, _jsonOptions);
                        
                        if (costs != null)
                        {
                            _logger.LogDebug("Model costs cache hit for provider: {Provider}", providerName);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return costs;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Model costs cache miss for provider, querying database: {Provider}", providerName);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbCosts = await databaseFallback(providerName);
                
                if (dbCosts != null && dbCosts.Any())
                {
                    // NOTE: Provider-based caching disabled as ModelCost doesn't contain provider info
                    // await SetProviderModelCostsAsync(providerName, dbCosts);
                    
                    // Also cache individual costs by pattern
                    foreach (var cost in dbCosts)
                    {
                        await SetModelCostAsync(cost);
                    }
                    
                    return dbCosts;
                }
                
                return dbCosts ?? new List<ModelCost>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Model Costs cache for provider, falling back to database: {Provider}", providerName);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback(providerName) ?? new List<ModelCost>();
            }
        }
        */

        /// <summary>
        /// Get Model Cost for a specific model ID by finding best matching pattern
        /// </summary>
        public async Task<ModelCost?> GetModelCostForModelIdAsync(
            string modelId, 
            Func<string, Task<ModelCost?>> databaseFallback)
        {
            try
            {
                // Try exact match first
                var exactKey = PatternKeyPrefix + modelId.ToLowerInvariant();
                var cachedValue = await _database.StringGetAsync(exactKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var cost = JsonSerializer.Deserialize<ModelCost>(jsonString, _jsonOptions);
                        if (cost != null)
                        {
                            _logger.LogDebug("Model cost cache hit for exact model ID: {ModelId}", modelId);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            await _database.StringIncrementAsync(STATS_PATTERN_MATCH_KEY);
                            return cost;
                        }
                    }
                }
                
                // If no exact match, fall back to database for pattern matching
                _logger.LogDebug("Model cost cache miss for model ID, querying database for pattern match: {ModelId}", modelId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbCost = await databaseFallback(modelId);
                
                if (dbCost != null)
                {
                    // Cache the result with the exact model ID for faster future lookups
                    var serialized = JsonSerializer.Serialize(dbCost, _jsonOptions);
                    await _database.StringSetAsync(exactKey, serialized, _defaultExpiry);
                    await _database.StringIncrementAsync(STATS_PATTERN_MATCH_KEY);
                    
                    return dbCost;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Model Cost cache for model ID, falling back to database: {ModelId}", modelId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback(modelId);
            }
        }

        /// <summary>
        /// Invalidate a specific Model Cost in cache
        /// </summary>
        public async Task InvalidateModelCostAsync(int modelCostId)
        {
            try
            {
                // We need to find and invalidate all keys related to this model cost
                // This includes the pattern key and any exact match keys
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: KeyPrefix + "*");
                
                foreach (var key in keys)
                {
                    var value = await _database.StringGetAsync(key);
                    if (value.HasValue)
                    {
                        try
                        {
                            var jsonString = (string?)value;
                            if (jsonString != null)
                            {
                                var cost = JsonSerializer.Deserialize<ModelCost>(jsonString, _jsonOptions);
                                if (cost?.Id == modelCostId)
                                {
                                    await _database.KeyDeleteAsync(key);
                                    
                                    // Note: Provider information is not stored in ModelCost entity
                                    // Provider-specific invalidation would require additional context
                                }
                            }
                        }
                        catch
                        {
                            // Skip malformed entries
                        }
                    }
                }
                
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                _logger.LogInformation("Model cost cache invalidated for ID: {ModelCostId}", modelCostId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Model Cost cache: {ModelCostId}", modelCostId);
            }
        }

        /*
        /// <summary>
        /// Invalidate all Model Costs for a provider
        /// NOTE: This method is disabled as ModelCost entity doesn't contain provider information
        /// </summary>
        public async Task InvalidateProviderModelCostsAsync(string providerName)
        {
            try
            {
                // Invalidate the provider costs list
                var providerKey = ProviderKeyPrefix + providerName.ToLowerInvariant();
                await _database.KeyDeleteAsync(providerKey);
                
                // Also invalidate individual cost entries for this provider
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: PatternKeyPrefix + "*");
                
                foreach (var key in keys)
                {
                    var value = await _database.StringGetAsync(key);
                    if (value.HasValue)
                    {
                        try
                        {
                            // Note: Provider information is not stored in ModelCost entity
                            // This method would need to be rethought or removed
                            // For now, we'll skip individual invalidation since we can't determine provider
                        }
                        catch
                        {
                            // Skip malformed entries
                        }
                    }
                }
                
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                _logger.LogInformation("Model costs cache invalidated for provider: {Provider}", providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating provider Model Costs cache: {Provider}", providerName);
            }
        }
        */

        /// <summary>
        /// Invalidate Model Cost by pattern
        /// </summary>
        public async Task InvalidateModelCostByPatternAsync(string modelIdPattern)
        {
            try
            {
                var patternKey = PatternKeyPrefix + modelIdPattern.ToLowerInvariant();
                await _database.KeyDeleteAsync(patternKey);
                
                // Also invalidate any exact match keys that might be affected
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: PatternKeyPrefix + "*");
                
                foreach (var key in keys)
                {
                    var keyString = key.ToString();
                    if (keyString != null && keyString.Contains(modelIdPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        await _database.KeyDeleteAsync(key);
                    }
                }
                
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                _logger.LogInformation("Model cost cache invalidated for pattern: {Pattern}", modelIdPattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Model Cost cache by pattern: {Pattern}", modelIdPattern);
            }
        }

        /// <summary>
        /// Clear all Model Cost entries from cache
        /// </summary>
        public async Task ClearAllModelCostsAsync()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: KeyPrefix + "*");
                
                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                _logger.LogWarning("All model cost cache entries cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all model cost cache entries");
            }
        }

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
            
            _logger.LogDebug("Model costs cached for provider: {Provider} ({Count} costs)", providerName, costs.Count);
        }
        */

        /// <summary>
        /// Batch invalidate multiple model costs
        /// </summary>
        public async Task<BatchInvalidationResult> InvalidateBatchAsync(
            IEnumerable<InvalidationRequest> requests, 
            CancellationToken cancellationToken = default)
        {
            var costIds = requests
                .Where(r => r.EntityType == CacheType.ModelCost.ToString())
                .Select(r => r.EntityId)
                .ToArray();
            
            if (costIds.Length == 0)
            {
                return new BatchInvalidationResult 
                { 
                    Success = true,
                    ProcessedCount = 0,
                    Duration = TimeSpan.Zero
                };
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Use Redis pipeline for batch delete
                var batch = _database.CreateBatch();
                var deleteTasks = new List<Task<bool>>();
                var keysToDelete = new List<string>();
                
                // For each cost ID, we need to find all related keys
                foreach (var costId in costIds)
                {
                    // Since we're working with pattern-based cache, we need to scan for keys
                    // This is less efficient than direct key deletion but necessary for pattern matching
                    if (int.TryParse(costId, out var id))
                    {
                        // Direct invalidation by ID
                        var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                        var keys = server.Keys(pattern: KeyPrefix + "*");
                        
                        foreach (var key in keys)
                        {
                            var value = await _database.StringGetAsync(key);
                            if (value.HasValue)
                            {
                                try
                                {
                                    var jsonString = (string?)value;
                                    if (jsonString != null)
                                    {
                                        var cost = JsonSerializer.Deserialize<ModelCost>(jsonString, _jsonOptions);
                                        if (cost?.Id == id)
                                        {
                                            keysToDelete.Add(key.ToString()!);
                                        }
                                    }
                                }
                                catch
                                {
                                    // Skip malformed entries
                                }
                            }
                        }
                    }
                    else
                    {
                        // Pattern-based invalidation
                        keysToDelete.Add(PatternKeyPrefix + costId.ToLowerInvariant());
                    }
                }
                
                // Delete all found keys in batch
                foreach (var key in keysToDelete)
                {
                    deleteTasks.Add(batch.KeyDeleteAsync(key));
                }
                
                // Execute batch
                batch.Execute();
                await Task.WhenAll(deleteTasks);
                
                // Update invalidation statistics
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY, keysToDelete.Count);
                
                // Publish batch invalidation message to other instances
                var batchMessage = new ModelCostBatchInvalidation
                {
                    CostIds = costIds,
                    Timestamp = DateTime.UtcNow
                };
                
                await _subscriber.PublishAsync(
                    RedisChannel.Literal(BatchInvalidationChannel), 
                    JsonSerializer.Serialize(batchMessage));
                
                stopwatch.Stop();
                
                _logger.LogInformation(
                    "Batch invalidated {Count} model costs in {Duration}ms",
                    costIds.Length, 
                    stopwatch.ElapsedMilliseconds);
                
                return new BatchInvalidationResult
                {
                    Success = true,
                    ProcessedCount = keysToDelete.Count,
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to batch invalidate model costs");
                
                return new BatchInvalidationResult
                {
                    Success = false,
                    ProcessedCount = 0,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        }

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
        /// Message for batch invalidation pub/sub
        /// </summary>
        private class ModelCostBatchInvalidation
        {
            public string[] CostIds { get; set; } = Array.Empty<string>();
            public DateTime Timestamp { get; set; }
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