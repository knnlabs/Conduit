using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based IP Filter cache with event-driven invalidation
    /// </summary>
    public class RedisIpFilterCache : IIpFilterCache
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisIpFilterCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1); // IP filters need quick updates
        private const string GlobalFiltersKey = "ipfilter:global";
        private const string VirtualKeyFilterPrefix = "ipfilter:vkey:";
        private const string IpCheckPrefix = "ipfilter:check:";
        
        // Statistics tracking keys
        private const string STATS_HIT_KEY = "conduit:cache:ipfilter:stats:hits";
        private const string STATS_MISS_KEY = "conduit:cache:ipfilter:stats:misses";
        private const string STATS_INVALIDATION_KEY = "conduit:cache:ipfilter:stats:invalidations";
        private const string STATS_RESET_TIME_KEY = "conduit:cache:ipfilter:stats:reset_time";
        private const string STATS_IP_CHECK_KEY = "conduit:cache:ipfilter:stats:ip_checks";

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisIpFilterCache(
            IConnectionMultiplexer redis,
            ILogger<RedisIpFilterCache> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            
            // Initialize stats reset time if not exists
            _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.ToString("O"), when: When.NotExists).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get all global IP filters from cache with database fallback
        /// </summary>
        public async Task<List<IpFilterEntity>> GetGlobalFiltersAsync(Func<Task<List<IpFilterEntity>>> databaseFallback)
        {
            try
            {
                var cachedValue = await _database.StringGetAsync(GlobalFiltersKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var filters = JsonSerializer.Deserialize<List<IpFilterEntity>>(jsonString, _jsonOptions);
                        
                        if (filters != null)
                        {
                            _logger.LogDebug("Global IP filters cache hit ({Count} filters)", filters.Count);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return filters;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Global IP filters cache miss, querying database");
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbFilters = await databaseFallback();
                
                if (dbFilters != null)
                {
                    // Cache the filters
                    await SetGlobalFiltersAsync(dbFilters);
                    return dbFilters;
                }
                
                return new List<IpFilterEntity>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing global IP filters cache, falling back to database");
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback() ?? new List<IpFilterEntity>();
            }
        }

        /// <summary>
        /// Get IP filters for a specific virtual key from cache with database fallback
        /// </summary>
        public async Task<List<IpFilterEntity>> GetVirtualKeyFiltersAsync(
            int virtualKeyId, 
            Func<int, Task<List<IpFilterEntity>>> databaseFallback)
        {
            var cacheKey = VirtualKeyFilterPrefix + virtualKeyId;
            
            try
            {
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var filters = JsonSerializer.Deserialize<List<IpFilterEntity>>(jsonString, _jsonOptions);
                        
                        if (filters != null)
                        {
                            _logger.LogDebug("Virtual key IP filters cache hit for key {VirtualKeyId} ({Count} filters)", 
                                virtualKeyId, filters.Count);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return filters;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Virtual key IP filters cache miss for key {VirtualKeyId}, querying database", virtualKeyId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbFilters = await databaseFallback(virtualKeyId);
                
                if (dbFilters != null)
                {
                    // Cache the filters
                    await SetVirtualKeyFiltersAsync(virtualKeyId, dbFilters);
                    return dbFilters;
                }
                
                return new List<IpFilterEntity>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing virtual key IP filters cache for key {VirtualKeyId}, falling back to database", 
                    virtualKeyId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback(virtualKeyId) ?? new List<IpFilterEntity>();
            }
        }

        /// <summary>
        /// Check if an IP address is allowed for a virtual key
        /// </summary>
        public async Task<bool> IsIpAllowedAsync(
            string ipAddress, 
            int? virtualKeyId, 
            Func<string, int?, Task<bool>> databaseFallback)
        {
            var cacheKey = IpCheckPrefix + ipAddress + (virtualKeyId.HasValue ? $":{virtualKeyId}" : ":global");
            
            try
            {
                // Try cached result first
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    _logger.LogDebug("IP check cache hit for {IP} (key: {VirtualKeyId})", ipAddress, virtualKeyId);
                    await _database.StringIncrementAsync(STATS_HIT_KEY);
                    await _database.StringIncrementAsync(STATS_IP_CHECK_KEY);
                    return cachedValue == "1";
                }
                
                // Cache miss - perform check
                _logger.LogDebug("IP check cache miss for {IP} (key: {VirtualKeyId}), performing check", ipAddress, virtualKeyId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var isAllowed = await databaseFallback(ipAddress, virtualKeyId);
                
                // Cache the result with shorter expiry for IP checks
                await _database.StringSetAsync(cacheKey, isAllowed ? "1" : "0", TimeSpan.FromMinutes(15));
                await _database.StringIncrementAsync(STATS_IP_CHECK_KEY);
                
                return isAllowed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking IP in cache for {IP} (key: {VirtualKeyId}), falling back to database", 
                    ipAddress, virtualKeyId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback(ipAddress, virtualKeyId);
            }
        }

        /// <summary>
        /// Invalidate a specific IP filter in cache
        /// </summary>
        public async Task InvalidateFilterAsync(int filterId)
        {
            try
            {
                // We need to invalidate all caches that might contain this filter
                // This includes global filters, virtual key filters, and IP check results
                
                // Clear all IP check results as they might be affected
                await ClearIpCheckResults();
                
                // Since we don't know if it's global or key-specific without querying,
                // we'll need to check both caches
                await InvalidateGlobalFiltersAsync();
                
                // For virtual key filters, we'd need to scan all keys
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var vkeyFilterKeys = server.Keys(pattern: VirtualKeyFilterPrefix + "*");
                
                foreach (var key in vkeyFilterKeys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                _logger.LogInformation("IP filter cache invalidated for filter ID: {FilterId}", filterId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating IP filter cache: {FilterId}", filterId);
            }
        }

        /// <summary>
        /// Invalidate all global IP filters in cache
        /// </summary>
        public async Task InvalidateGlobalFiltersAsync()
        {
            try
            {
                await _database.KeyDeleteAsync(GlobalFiltersKey);
                await ClearIpCheckResults(); // IP checks depend on filters
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                
                _logger.LogInformation("Global IP filters cache invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating global IP filters cache");
            }
        }

        /// <summary>
        /// Invalidate all IP filters for a specific virtual key
        /// </summary>
        public async Task InvalidateVirtualKeyFiltersAsync(int virtualKeyId)
        {
            try
            {
                var cacheKey = VirtualKeyFilterPrefix + virtualKeyId;
                await _database.KeyDeleteAsync(cacheKey);
                
                // Clear IP check results for this virtual key
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var ipCheckKeys = server.Keys(pattern: IpCheckPrefix + $"*:{virtualKeyId}");
                
                foreach (var key in ipCheckKeys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                _logger.LogInformation("Virtual key IP filters cache invalidated for key: {VirtualKeyId}", virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating virtual key IP filters cache: {VirtualKeyId}", virtualKeyId);
            }
        }

        /// <summary>
        /// Clear all IP filter entries from cache
        /// </summary>
        public async Task ClearAllFiltersAsync()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                
                // Clear all filter caches
                var filterKeys = server.Keys(pattern: "ipfilter:*");
                foreach (var key in filterKeys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                _logger.LogWarning("All IP filter cache entries cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all IP filter cache entries");
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<IpFilterCacheStats> GetStatsAsync()
        {
            try
            {
                var hits = await _database.StringGetAsync(STATS_HIT_KEY);
                var misses = await _database.StringGetAsync(STATS_MISS_KEY);
                var invalidations = await _database.StringGetAsync(STATS_INVALIDATION_KEY);
                var ipChecks = await _database.StringGetAsync(STATS_IP_CHECK_KEY);
                var resetTime = await _database.StringGetAsync(STATS_RESET_TIME_KEY);
                
                // Count entries
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var filterKeys = server.Keys(pattern: "ipfilter:*");
                var entryCount = 0L;
                var globalCount = 0L;
                var keySpecificCount = 0L;
                
                foreach (var key in filterKeys)
                {
                    entryCount++;
                    var keyString = key.ToString();
                    if (keyString?.Contains(":global") == true)
                        globalCount++;
                    else if (keyString?.Contains(":vkey:") == true)
                        keySpecificCount++;
                }
                
                return new IpFilterCacheStats
                {
                    HitCount = hits.HasValue ? (long)hits : 0,
                    MissCount = misses.HasValue ? (long)misses : 0,
                    InvalidationCount = invalidations.HasValue ? (long)invalidations : 0,
                    IpCheckCount = ipChecks.HasValue ? (long)ipChecks : 0,
                    LastResetTime = resetTime.HasValue && DateTime.TryParse(resetTime, out var time) ? time : DateTime.UtcNow,
                    EntryCount = entryCount,
                    GlobalFilterCount = globalCount,
                    KeySpecificFilterCount = keySpecificCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP filter cache statistics");
                return new IpFilterCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task SetGlobalFiltersAsync(List<IpFilterEntity> filters)
        {
            var serialized = JsonSerializer.Serialize(filters, _jsonOptions);
            await _database.StringSetAsync(GlobalFiltersKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Global IP filters cached ({Count} filters)", filters.Count);
        }

        private async Task SetVirtualKeyFiltersAsync(int virtualKeyId, List<IpFilterEntity> filters)
        {
            var cacheKey = VirtualKeyFilterPrefix + virtualKeyId;
            var serialized = JsonSerializer.Serialize(filters, _jsonOptions);
            await _database.StringSetAsync(cacheKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Virtual key IP filters cached for key {VirtualKeyId} ({Count} filters)", 
                virtualKeyId, filters.Count);
        }

        private async Task ClearIpCheckResults()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var ipCheckKeys = server.Keys(pattern: IpCheckPrefix + "*");
                
                foreach (var key in ipCheckKeys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                _logger.LogDebug("IP check cache results cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing IP check cache results");
            }
        }
    }
}