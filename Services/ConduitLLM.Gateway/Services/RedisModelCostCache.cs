using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based Model Cost cache with event-driven invalidation
    /// </summary>
    public partial class RedisModelCostCache : IModelCostCache, IBatchInvalidatable, IDisposable
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisModelCostCache> _logger;
        private readonly IDistributedCachePopulator _cachePopulator;
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
        {
            _database = redis.GetDatabase();
            _subscriber = redis.GetSubscriber();
            _logger = logger;
            _cachePopulator = cachePopulator;

            // Initialize stats reset time if not exists (fire-and-forget, non-blocking)
            _ = _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.ToString("O"), when: When.NotExists)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogWarning(t.Exception, "Failed to initialize stats reset time");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);

            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(InvalidationChannel), OnCostInvalidated);
            _subscriber.Subscribe(RedisChannel.Literal(BatchInvalidationChannel), OnBatchInvalidated);

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
                            Interlocked.Increment(ref _statsBuffer.Hits);
                            return cost;
                        }
                    }
                }
                
                // Cache miss - use stampede prevention to avoid multiple concurrent DB queries
                _logger.LogDebug("Model cost cache miss for pattern, querying database: {Pattern}", modelIdPattern);
                Interlocked.Increment(ref _statsBuffer.Misses);

                var dbCost = await _cachePopulator.GetOrPopulateAsync(
                    lockKey: $"populate:modelcost:pattern:{modelIdPattern.ToLowerInvariant()}",
                    cacheCheck: async () =>
                    {
                        // Re-check cache in case another instance populated it
                        var cached = await _database.StringGetAsync(cacheKey);
                        if (cached.HasValue)
                        {
                            var jsonStr = (string?)cached;
                            if (jsonStr is not null)
                            {
                                return JsonSerializer.Deserialize<ModelCost>(jsonStr, _jsonOptions);
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
                _logger.LogError(ex, "Error accessing Model Cost cache for pattern, falling back to database: {Pattern}", modelIdPattern);
                Interlocked.Increment(ref _statsBuffer.Misses);
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
                
                if (dbCosts != null && dbCosts.Count() > 0)
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
                            Interlocked.Increment(ref _statsBuffer.Hits);
                            Interlocked.Increment(ref _statsBuffer.PatternMatches);
                            return cost;
                        }
                    }
                }
                
                // If no exact match, use stampede prevention to avoid multiple concurrent DB queries
                _logger.LogDebug("Model cost cache miss for model ID, querying database for pattern match: {ModelId}", modelId);
                Interlocked.Increment(ref _statsBuffer.Misses);

                var dbCost = await _cachePopulator.GetOrPopulateAsync(
                    lockKey: $"populate:modelcost:modelid:{modelId.ToLowerInvariant()}",
                    cacheCheck: async () =>
                    {
                        // Re-check cache in case another instance populated it
                        var cached = await _database.StringGetAsync(exactKey);
                        if (cached.HasValue)
                        {
                            var jsonStr = (string?)cached;
                            if (jsonStr is not null)
                            {
                                return JsonSerializer.Deserialize<ModelCost>(jsonStr, _jsonOptions);
                            }
                        }
                        return null;
                    },
                    factory: () => databaseFallback(modelId));

                if (dbCost != null)
                {
                    // Cache the result with the exact model ID for faster future lookups
                    var serialized = JsonSerializer.Serialize(dbCost, _jsonOptions);
                    await _database.StringSetAsync(exactKey, serialized, _defaultExpiry);
                    Interlocked.Increment(ref _statsBuffer.PatternMatches);

                    return dbCost;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Model Cost cache for model ID, falling back to database: {ModelId}", modelId);
                Interlocked.Increment(ref _statsBuffer.Misses);
                return await databaseFallback(modelId);
            }
        }

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

                var batch = _database.CreateBatch();
                var tasks = new List<Task>();

                if (hits > 0) tasks.Add(batch.StringIncrementAsync(STATS_HIT_KEY, hits));
                if (misses > 0) tasks.Add(batch.StringIncrementAsync(STATS_MISS_KEY, misses));
                if (patternMatches > 0) tasks.Add(batch.StringIncrementAsync(STATS_PATTERN_MATCH_KEY, patternMatches));
                if (invalidations > 0) tasks.Add(batch.StringIncrementAsync(STATS_INVALIDATION_KEY, invalidations));

                batch.Execute();
                await Task.WhenAll(tasks);

                _logger.LogDebug("Flushed model cost cache stats: Hits={Hits}, Misses={Misses}, Patterns={Patterns}, Invalidations={Invalidations}",
                    hits, misses, patternMatches, invalidations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing model cost cache statistics");
            }
            finally
            {
                _flushLock.Release();
            }
        }

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
                        if (hits > 0) tasks.Add(_database.StringIncrementAsync(STATS_HIT_KEY, hits));
                        if (misses > 0) tasks.Add(_database.StringIncrementAsync(STATS_MISS_KEY, misses));
                        if (patternMatches > 0) tasks.Add(_database.StringIncrementAsync(STATS_PATTERN_MATCH_KEY, patternMatches));
                        if (invalidations > 0) tasks.Add(_database.StringIncrementAsync(STATS_INVALIDATION_KEY, invalidations));
                        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));

                        _logger.LogDebug("Final flush of model cost cache stats on dispose: Hits={Hits}, Misses={Misses}, Patterns={Patterns}, Invalidations={Invalidations}",
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
                _logger.LogWarning(ex, "Error during final statistics flush on dispose");
            }

            _flushLock.Dispose();
        }
    }
}