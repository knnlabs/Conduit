using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Serialization;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based Model Cost cache with event-driven invalidation
    /// </summary>
    public partial class RedisModelCostCache : BufferedStatsRedisCacheBase, IModelCostCache, IBatchInvalidatable
    {
        private readonly IDistributedCachePopulator _cachePopulator;
        private readonly ISubscriber _subscriber;

        protected override string ServiceName => CacheKeys.Stats.ModelCostService;

        // Custom counter for pattern match lookups (beyond standard hits/misses/invalidations)
        private long _bufferedPatternMatches;

        private static ModelCost? DeserializeModelCost(string json) =>
            JsonSerializer.Deserialize(json, GatewayRedisJsonContext.Default.ModelCost);

        private static string SerializeModelCost(ModelCost value) =>
            JsonSerializer.Serialize(value, GatewayRedisJsonContext.Default.ModelCost);

        private static ModelCostBatchInvalidation? DeserializeBatchInvalidation(string json) =>
            JsonSerializer.Deserialize(
                json,
                GatewayRedisJsonContext.Default.ModelCostBatchInvalidation);

        private static string SerializeBatchInvalidation(ModelCostBatchInvalidation value) =>
            JsonSerializer.Serialize(
                value,
                GatewayRedisJsonContext.Default.ModelCostBatchInvalidation);

        public RedisModelCostCache(
            IConnectionMultiplexer redis,
            ILogger<RedisModelCostCache> logger,
            IDistributedCachePopulator cachePopulator)
            : base(redis, logger, TimeSpan.FromHours(6))
        {
            _cachePopulator = cachePopulator;
            _subscriber = redis.GetSubscriber();

            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(CacheKeys.ModelCost.InvalidationChannel), OnCostInvalidated);
            _subscriber.Subscribe(RedisChannel.Literal(CacheKeys.ModelCost.BatchInvalidationChannel), OnBatchInvalidated);
        }

        /// <summary>
        /// Get Model Cost by pattern from cache with database fallback
        /// </summary>
        public async Task<ModelCost?> GetModelCostByPatternAsync(
            string modelIdPattern,
            Func<string, Task<ModelCost?>> databaseFallback)
        {
            var cacheKey = CacheKeys.ModelCost.PatternPrefix + modelIdPattern.ToLowerInvariant();

            try
            {
                var cachedValue = await Database.StringGetAsync(cacheKey);

                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var cost = DeserializeModelCost(jsonString);

                        if (cost != null)
                        {
                            Logger.LogDebug("Model cost cache hit for pattern: {Pattern}", modelIdPattern);
                            Interlocked.Increment(ref _bufferedPatternMatches);
                            await TrackHitAsync(ServiceName);
                            GatewayCacheMetrics.RecordHit("modelcost");
                            return cost;
                        }
                    }
                }

                // Cache miss - use stampede prevention to avoid multiple concurrent DB queries
                Logger.LogDebug("Model cost cache miss for pattern, querying database: {Pattern}", modelIdPattern);
                await TrackMissAsync(ServiceName);
                GatewayCacheMetrics.RecordMiss("modelcost");

                var dbCost = await _cachePopulator.GetOrPopulateAsync(
                    lockKey: $"populate:modelcost:pattern:{modelIdPattern.ToLowerInvariant()}",
                    cacheCheck: async () =>
                    {
                        // Re-check cache in case another instance populated it
                        var cached = await Database.StringGetAsync(cacheKey);
                        if (cached.HasValue)
                        {
                            var jsonStr = (string?)cached;
                            if (jsonStr is not null)
                            {
                                return DeserializeModelCost(jsonStr);
                            }
                        }
                        return null;
                    },
                    factory: () => databaseFallback(modelIdPattern));

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
                Logger.LogError(ex, "Error accessing Model Cost cache for pattern, falling back to database: {Pattern}", modelIdPattern);
                await TrackMissAsync(ServiceName);
                GatewayCacheMetrics.RecordError("modelcost", "get");
                return await databaseFallback(modelIdPattern);
            }
        }

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
                var exactKey = CacheKeys.ModelCost.PatternPrefix + modelId.ToLowerInvariant();
                var cachedValue = await Database.StringGetAsync(exactKey);

                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var cost = DeserializeModelCost(jsonString);
                        if (cost != null)
                        {
                            Logger.LogDebug("Model cost cache hit for exact model ID: {ModelId}", modelId);
                            Interlocked.Increment(ref _bufferedPatternMatches);
                            await TrackHitAsync(ServiceName);
                            GatewayCacheMetrics.RecordHit("modelcost");
                            return cost;
                        }
                    }
                }

                // If no exact match, use stampede prevention to avoid multiple concurrent DB queries
                Logger.LogDebug("Model cost cache miss for model ID, querying database for pattern match: {ModelId}", modelId);
                await TrackMissAsync(ServiceName);
                GatewayCacheMetrics.RecordMiss("modelcost");

                var dbCost = await _cachePopulator.GetOrPopulateAsync(
                    lockKey: $"populate:modelcost:modelid:{modelId.ToLowerInvariant()}",
                    cacheCheck: async () =>
                    {
                        // Re-check cache in case another instance populated it
                        var cached = await Database.StringGetAsync(exactKey);
                        if (cached.HasValue)
                        {
                            var jsonStr = (string?)cached;
                            if (jsonStr is not null)
                            {
                                return DeserializeModelCost(jsonStr);
                            }
                        }
                        return null;
                    },
                    factory: () => databaseFallback(modelId));

                if (dbCost != null)
                {
                    // Cache the result with the exact model ID for faster future lookups
                    var serialized = SerializeModelCost(dbCost);
                    await Database.StringSetAsync(exactKey, serialized, DefaultExpiry);
                    Interlocked.Increment(ref _bufferedPatternMatches);

                    return dbCost;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error accessing Model Cost cache for model ID, falling back to database: {ModelId}", modelId);
                await TrackMissAsync(ServiceName);
                GatewayCacheMetrics.RecordError("modelcost", "get");
                return await databaseFallback(modelId);
            }
        }

        #region Custom PatternMatches Counter

        protected override bool HasPendingCustomStats()
            => Interlocked.Read(ref _bufferedPatternMatches) > 0;

        protected override void OnFlush(IBatch batch, List<Task> tasks)
        {
            var patternMatches = Interlocked.Exchange(ref _bufferedPatternMatches, 0);
            if (patternMatches > 0)
            {
                tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.PatternMatches(), patternMatches));
            }
        }

        protected override void OnFinalFlush(List<Task> tasks)
        {
            var patternMatches = Interlocked.Exchange(ref _bufferedPatternMatches, 0);
            if (patternMatches > 0)
            {
                tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.PatternMatches(), patternMatches));
            }
        }

        #endregion
    }
}
