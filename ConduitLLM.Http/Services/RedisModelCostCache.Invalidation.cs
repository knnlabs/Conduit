using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Model Cost cache - Invalidation operations
    /// </summary>
    public partial class RedisModelCostCache
    {
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
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY, keysToDelete.Count());
                
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
                    ProcessedCount = keysToDelete.Count(),
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
        /// Message for batch invalidation pub/sub
        /// </summary>
        private class ModelCostBatchInvalidation
        {
            public string[] CostIds { get; set; } = Array.Empty<string>();
            public DateTime Timestamp { get; set; }
        }
    }
}