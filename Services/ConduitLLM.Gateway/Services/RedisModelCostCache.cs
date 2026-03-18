using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based Model Cost cache with event-driven invalidation
    /// </summary>
    public partial class RedisModelCostCache : RedisCacheServiceBase, IModelCostCache, IBatchInvalidatable, IDisposable
    {
        private readonly IDistributedCachePopulator _cachePopulator;
        private readonly ISubscriber _subscriber;

        private static readonly string ServiceName = CacheKeys.Stats.ModelCostService;

        // Statistics batching - buffer locally and flush periodically to reduce Redis round-trips
        private readonly StatisticsBuffer _statsBuffer = new();
        private readonly Timer _flushTimer;
        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Thread-safe buffer for statistics counters
        /// </summary>
        private class StatisticsBuffer
        {
            public long Hits;
            public long Misses;
            public long PatternMatches;
            public long Invalidations;

            public (long hits, long misses, long patternMatches, long invalidations) GetAndReset()
            {
                var hits = Interlocked.Exchange(ref Hits, 0);
                var misses = Interlocked.Exchange(ref Misses, 0);
                var patternMatches = Interlocked.Exchange(ref PatternMatches, 0);
                var invalidations = Interlocked.Exchange(ref Invalidations, 0);
                return (hits, misses, patternMatches, invalidations);
            }
        }

        public RedisModelCostCache(
            IConnectionMultiplexer redis,
            ILogger<RedisModelCostCache> logger,
            IDistributedCachePopulator cachePopulator)
            : base(redis, logger, TimeSpan.FromHours(6))
        {
            _cachePopulator = cachePopulator;
            _subscriber = redis.GetSubscriber();

            InitializeStatsResetTime(ServiceName);

            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(CacheKeys.ModelCost.InvalidationChannel), OnCostInvalidated);
            _subscriber.Subscribe(RedisChannel.Literal(CacheKeys.ModelCost.BatchInvalidationChannel), OnBatchInvalidated);

            // Initialize statistics flush timer
            _flushTimer = new Timer(FlushStatisticsCallback, null, _flushInterval, _flushInterval);
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
                        var cost = JsonSerializer.Deserialize<ModelCost>(jsonString, JsonOptions);

                        if (cost != null)
                        {
                            Logger.LogDebug("Model cost cache hit for pattern: {Pattern}", modelIdPattern);
                            Interlocked.Increment(ref _statsBuffer.Hits);
                            GatewayCacheMetrics.RecordHit("modelcost");
                            return cost;
                        }
                    }
                }

                // Cache miss - use stampede prevention to avoid multiple concurrent DB queries
                Logger.LogDebug("Model cost cache miss for pattern, querying database: {Pattern}", modelIdPattern);
                Interlocked.Increment(ref _statsBuffer.Misses);
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
                                return JsonSerializer.Deserialize<ModelCost>(jsonStr, JsonOptions);
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
                Interlocked.Increment(ref _statsBuffer.Misses);
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
                        var cost = JsonSerializer.Deserialize<ModelCost>(jsonString, JsonOptions);
                        if (cost != null)
                        {
                            Logger.LogDebug("Model cost cache hit for exact model ID: {ModelId}", modelId);
                            Interlocked.Increment(ref _statsBuffer.Hits);
                            Interlocked.Increment(ref _statsBuffer.PatternMatches);
                            GatewayCacheMetrics.RecordHit("modelcost");
                            return cost;
                        }
                    }
                }

                // If no exact match, use stampede prevention to avoid multiple concurrent DB queries
                Logger.LogDebug("Model cost cache miss for model ID, querying database for pattern match: {ModelId}", modelId);
                Interlocked.Increment(ref _statsBuffer.Misses);
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
                                return JsonSerializer.Deserialize<ModelCost>(jsonStr, JsonOptions);
                            }
                        }
                        return null;
                    },
                    factory: () => databaseFallback(modelId));

                if (dbCost != null)
                {
                    // Cache the result with the exact model ID for faster future lookups
                    var serialized = JsonSerializer.Serialize(dbCost, JsonOptions);
                    await Database.StringSetAsync(exactKey, serialized, DefaultExpiry);
                    Interlocked.Increment(ref _statsBuffer.PatternMatches);

                    return dbCost;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error accessing Model Cost cache for model ID, falling back to database: {ModelId}", modelId);
                Interlocked.Increment(ref _statsBuffer.Misses);
                GatewayCacheMetrics.RecordError("modelcost", "get");
                return await databaseFallback(modelId);
            }
        }

        #region Buffered Stats Override

        protected override Task TrackHitAsync(string serviceName)
        {
            Interlocked.Increment(ref _statsBuffer.Hits);
            return Task.CompletedTask;
        }

        protected override Task TrackMissAsync(string serviceName)
        {
            Interlocked.Increment(ref _statsBuffer.Misses);
            return Task.CompletedTask;
        }

        protected override Task TrackInvalidationAsync(string serviceName, long count = 1)
        {
            Interlocked.Add(ref _statsBuffer.Invalidations, count);
            return Task.CompletedTask;
        }

        #endregion

        #region Statistics Flush

        /// <summary>
        /// Timer callback for periodic statistics flush
        /// </summary>
        private void FlushStatisticsCallback(object? state) => _ = FlushStatisticsAsync();

        /// <summary>
        /// Flush buffered statistics to Redis
        /// </summary>
        private async Task FlushStatisticsAsync()
        {
            if (_disposed) return;
            if (!await _flushLock.WaitAsync(0)) return;

            try
            {
                var (hits, misses, patternMatches, invalidations) = _statsBuffer.GetAndReset();
                if (hits == 0 && misses == 0 && patternMatches == 0 && invalidations == 0) return;

                var batch = Database.CreateBatch();
                var tasks = new List<Task>();

                if (hits > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Hits(ServiceName), hits));
                if (misses > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Misses(ServiceName), misses));
                if (patternMatches > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.PatternMatches(), patternMatches));
                if (invalidations > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Invalidations(ServiceName), invalidations));

                batch.Execute();
                await Task.WhenAll(tasks);

                Logger.LogDebug("Flushed model cost cache stats: Hits={Hits}, Misses={Misses}, Patterns={Patterns}, Invalidations={Invalidations}",
                    hits, misses, patternMatches, invalidations);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error flushing model cost cache statistics");
            }
            finally
            {
                _flushLock.Release();
            }
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose resources and flush remaining statistics
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _flushTimer.Change(Timeout.Infinite, 0);
            _flushTimer.Dispose();

            // Final synchronous flush
            try
            {
                _flushLock.Wait(TimeSpan.FromSeconds(5));
                try
                {
                    var (hits, misses, patternMatches, invalidations) = _statsBuffer.GetAndReset();
                    if (hits > 0 || misses > 0 || patternMatches > 0 || invalidations > 0)
                    {
                        var tasks = new List<Task>();
                        if (hits > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Hits(ServiceName), hits));
                        if (misses > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Misses(ServiceName), misses));
                        if (patternMatches > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.PatternMatches(), patternMatches));
                        if (invalidations > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Invalidations(ServiceName), invalidations));
                        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));

                        Logger.LogDebug("Final flush of model cost cache stats on dispose: Hits={Hits}, Misses={Misses}, Patterns={Patterns}, Invalidations={Invalidations}",
                            hits, misses, patternMatches, invalidations);
                    }
                }
                finally
                {
                    _flushLock.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error during final statistics flush on dispose");
            }

            _flushLock.Dispose();
        }

        #endregion
    }
}
