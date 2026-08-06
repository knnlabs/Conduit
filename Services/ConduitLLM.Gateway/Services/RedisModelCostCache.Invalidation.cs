using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Services
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
                var server = Database.Multiplexer.GetServer(Database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: CacheKeys.ModelCost.Prefix + "*");

                foreach (var key in keys)
                {
                    var value = await Database.StringGetAsync(key);
                    if (value.HasValue)
                    {
                        try
                        {
                            var jsonString = (string?)value;
                            if (jsonString != null)
                            {
                                var cost = DeserializeModelCost(jsonString);
                                if (cost?.Id == modelCostId)
                                {
                                    await Database.KeyDeleteAsync(key);
                                }
                            }
                        }
                        catch
                        {
                            // Skip malformed entries
                        }
                    }
                }

                await TrackInvalidationAsync(ServiceName);
                Logger.LogDebug("Model cost cache invalidated for ID: {ModelCostId}", modelCostId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating Model Cost cache: {ModelCostId}", modelCostId);
            }
        }

        /// <summary>
        /// Invalidate Model Cost by pattern
        /// </summary>
        public async Task InvalidateModelCostByPatternAsync(string modelIdPattern)
        {
            try
            {
                var patternKey = CacheKeys.ModelCost.PatternPrefix + modelIdPattern.ToLowerInvariant();
                await Database.KeyDeleteAsync(patternKey);

                // Also invalidate any exact match keys that might be affected
                var server = Database.Multiplexer.GetServer(Database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: CacheKeys.ModelCost.PatternPrefix + "*");

                foreach (var key in keys)
                {
                    var keyString = key.ToString();
                    if (keyString != null && keyString.Contains(modelIdPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        await Database.KeyDeleteAsync(key);
                    }
                }

                await TrackInvalidationAsync(ServiceName);
                Logger.LogDebug("Model cost cache invalidated for pattern: {Pattern}", modelIdPattern);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating Model Cost cache by pattern: {Pattern}", modelIdPattern);
            }
        }

        /// <summary>
        /// Clear all Model Cost entries from cache
        /// </summary>
        public async Task ClearAllModelCostsAsync()
        {
            try
            {
                await ClearAllByPatternAsync(CacheKeys.ModelCost.Prefix + "*");
                Logger.LogWarning("All model cost cache entries cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing all model cost cache entries");
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
                var batch = Database.CreateBatch();
                var deleteTasks = new List<Task<bool>>();
                var keysToDelete = new List<string>();

                // For each cost ID, we need to find all related keys
                foreach (var costId in costIds)
                {
                    if (int.TryParse(costId, out var id))
                    {
                        // Direct invalidation by ID
                        var server = Database.Multiplexer.GetServer(Database.Multiplexer.GetEndPoints()[0]);
                        var keys = server.Keys(pattern: CacheKeys.ModelCost.Prefix + "*");

                        foreach (var key in keys)
                        {
                            var value = await Database.StringGetAsync(key);
                            if (value.HasValue)
                            {
                                try
                                {
                                    var jsonString = (string?)value;
                                    if (jsonString != null)
                                    {
                                        var cost = DeserializeModelCost(jsonString);
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
                        keysToDelete.Add(CacheKeys.ModelCost.PatternPrefix + costId.ToLowerInvariant());
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
                await TrackInvalidationAsync(ServiceName, keysToDelete.Count);

                // Publish batch invalidation message to other instances
                var batchMessage = new ModelCostBatchInvalidation
                {
                    CostIds = costIds,
                    Timestamp = DateTime.UtcNow
                };

                await _subscriber.PublishAsync(
                    RedisChannel.Literal(CacheKeys.ModelCost.BatchInvalidationChannel),
                    SerializeBatchInvalidation(batchMessage));

                stopwatch.Stop();

                Logger.LogInformation(
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
                Logger.LogError(ex, "Failed to batch invalidate model costs");

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
        internal sealed class ModelCostBatchInvalidation
        {
            public string[] CostIds { get; set; } = Array.Empty<string>();
            public DateTime Timestamp { get; set; }
        }
    }
}
