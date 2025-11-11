using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Model Cost cache with event-driven invalidation
    /// </summary>
    public partial class RedisModelCostCache : IModelCostCache, IBatchInvalidatable
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



    }
}