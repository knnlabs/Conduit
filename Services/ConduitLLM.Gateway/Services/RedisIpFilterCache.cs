using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based IP Filter cache with event-driven invalidation
    /// </summary>
    public class RedisIpFilterCache : RedisCacheServiceBase, IIpFilterCache
    {
        private static readonly string ServiceName = CacheKeys.Stats.IpFilterService;

        public RedisIpFilterCache(
            IConnectionMultiplexer redis,
            ILogger<RedisIpFilterCache> logger)
            : base(redis, logger, TimeSpan.FromHours(1))
        {
            InitializeStatsResetTime(ServiceName);
        }

        /// <summary>
        /// Get all global IP filters from cache with database fallback
        /// </summary>
        public async Task<List<IpFilterEntity>> GetGlobalFiltersAsync(Func<Task<List<IpFilterEntity>>> databaseFallback)
        {
            var result = await GetOrFallbackAsync<List<IpFilterEntity>>(
                CacheKeys.IpFilter.GlobalFilters,
                ServiceName,
                async () => await databaseFallback() as List<IpFilterEntity>,
                debugLabel: "Global IP filters");

            return result ?? new List<IpFilterEntity>();
        }

        /// <summary>
        /// Get IP filters for a specific virtual key from cache with database fallback
        /// </summary>
        public async Task<List<IpFilterEntity>> GetVirtualKeyFiltersAsync(
            int virtualKeyId,
            Func<int, Task<List<IpFilterEntity>>> databaseFallback)
        {
            var cacheKey = CacheKeys.IpFilter.ByVirtualKey(virtualKeyId);

            var result = await GetOrFallbackAsync<List<IpFilterEntity>>(
                cacheKey,
                ServiceName,
                async () => await databaseFallback(virtualKeyId) as List<IpFilterEntity>,
                debugLabel: $"Virtual key IP filters for key {virtualKeyId}");

            return result ?? new List<IpFilterEntity>();
        }

        /// <summary>
        /// Check if an IP address is allowed for a virtual key
        /// </summary>
        public async Task<bool> IsIpAllowedAsync(
            string ipAddress,
            int? virtualKeyId,
            Func<string, int?, Task<bool>> databaseFallback)
        {
            var cacheKey = CacheKeys.IpFilter.CheckResult(ipAddress, virtualKeyId);

            try
            {
                // Try cached result first
                var cachedValue = await Database.StringGetAsync(cacheKey);

                if (cachedValue.HasValue)
                {
                    Logger.LogDebug("IP check cache hit for {IP} (key: {VirtualKeyId})", ipAddress, virtualKeyId);
                    await TrackHitAsync(ServiceName);
                    await Database.StringIncrementAsync(CacheKeys.Stats.IpChecks());
                    return cachedValue == "1";
                }

                // Cache miss - perform check
                Logger.LogDebug("IP check cache miss for {IP} (key: {VirtualKeyId}), performing check", ipAddress, virtualKeyId);
                await TrackMissAsync(ServiceName);

                var isAllowed = await databaseFallback(ipAddress, virtualKeyId);

                // Cache the result with shorter expiry for IP checks
                await Database.StringSetAsync(cacheKey, isAllowed ? "1" : "0", TimeSpan.FromMinutes(15));
                await Database.StringIncrementAsync(CacheKeys.Stats.IpChecks());

                return isAllowed;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking IP in cache for {IP} (key: {VirtualKeyId}), falling back to database",
                    ipAddress, virtualKeyId);
                await TrackMissAsync(ServiceName);
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
                // Clear all IP check results as they might be affected
                await ClearIpCheckResults();

                // Invalidate global filters
                await InvalidateGlobalFiltersAsync();

                // For virtual key filters, scan and delete all
                await ClearAllByPatternAsync(CacheKeys.IpFilter.VirtualKeyPrefix + "*");

                await TrackInvalidationAsync(ServiceName);
                Logger.LogDebug("IP filter cache invalidated for filter ID: {FilterId}", filterId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating IP filter cache: {FilterId}", filterId);
            }
        }

        /// <summary>
        /// Invalidate all global IP filters in cache
        /// </summary>
        public async Task InvalidateGlobalFiltersAsync()
        {
            try
            {
                await Database.KeyDeleteAsync(CacheKeys.IpFilter.GlobalFilters);
                await ClearIpCheckResults(); // IP checks depend on filters
                await TrackInvalidationAsync(ServiceName);

                Logger.LogDebug("Global IP filters cache invalidated");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating global IP filters cache");
            }
        }

        /// <summary>
        /// Invalidate all IP filters for a specific virtual key
        /// </summary>
        public async Task InvalidateVirtualKeyFiltersAsync(int virtualKeyId)
        {
            try
            {
                var cacheKey = CacheKeys.IpFilter.ByVirtualKey(virtualKeyId);
                await Database.KeyDeleteAsync(cacheKey);

                // Clear IP check results for this virtual key
                await ClearAllByPatternAsync(CacheKeys.IpFilter.CheckPrefix + $"*:{virtualKeyId}");

                await TrackInvalidationAsync(ServiceName);
                Logger.LogDebug("Virtual key IP filters cache invalidated for key: {VirtualKeyId}", virtualKeyId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating virtual key IP filters cache: {VirtualKeyId}", virtualKeyId);
            }
        }

        /// <summary>
        /// Clear all IP filter entries from cache
        /// </summary>
        public async Task ClearAllFiltersAsync()
        {
            try
            {
                await ClearAllByPatternAsync("ipfilter:*");
                Logger.LogWarning("All IP filter cache entries cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing all IP filter cache entries");
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<IpFilterCacheStats> GetStatsAsync()
        {
            try
            {
                var (hits, misses, invalidations, resetTime) = await GetBaseStatsAsync(ServiceName);
                var ipChecks = await Database.StringGetAsync(CacheKeys.Stats.IpChecks());

                // Count entries with category breakdown
                var server = Database.Multiplexer.GetServer(Database.Multiplexer.GetEndPoints()[0]);
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
                    HitCount = hits,
                    MissCount = misses,
                    InvalidationCount = invalidations,
                    IpCheckCount = ipChecks.HasValue ? (long)ipChecks : 0,
                    LastResetTime = resetTime,
                    EntryCount = entryCount,
                    GlobalFilterCount = globalCount,
                    KeySpecificFilterCount = keySpecificCount
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting IP filter cache statistics");
                return new IpFilterCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task ClearIpCheckResults()
        {
            try
            {
                await ClearAllByPatternAsync(CacheKeys.IpFilter.CheckPrefix + "*");
                Logger.LogDebug("IP check cache results cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing IP check cache results");
            }
        }
    }
}
