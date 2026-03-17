using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based Provider Credential cache with event-driven invalidation
    /// </summary>
    public class RedisProviderCache : RedisCacheServiceBase, IProviderCache
    {
        private readonly IDistributedCachePopulator _cachePopulator;

        private static readonly string ServiceName = CacheKeys.Stats.ProviderService;

        public RedisProviderCache(
            IConnectionMultiplexer redis,
            ILogger<RedisProviderCache> logger,
            IDistributedCachePopulator cachePopulator)
            : base(redis, logger, TimeSpan.FromHours(1))
        {
            _cachePopulator = cachePopulator;
            InitializeStatsResetTime(ServiceName);
        }

        /// <summary>
        /// Get Provider Credential from cache with database fallback
        /// </summary>
        public async Task<CachedProvider?> GetProviderAsync(
            int providerId,
            Func<int, Task<CachedProvider?>> databaseFallback)
        {
            var cacheKey = CacheKeys.Provider.ById(providerId);

            try
            {
                var cachedValue = await Database.StringGetAsync(cacheKey);

                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var credential = JsonSerializer.Deserialize<CachedProvider>(jsonString, JsonOptions);

                        if (credential != null)
                        {
                            Logger.LogDebug("Provider credential cache hit: {ProviderId}", providerId);
                            await TrackHitAsync(ServiceName);
                            return credential;
                        }
                    }
                }

                // Cache miss - use stampede prevention to avoid multiple concurrent DB queries
                Logger.LogDebug("Provider credential cache miss, querying database: {ProviderId}", providerId);
                await TrackMissAsync(ServiceName);

                var dbCredential = await _cachePopulator.GetOrPopulateAsync(
                    lockKey: $"populate:provider:{providerId}",
                    cacheCheck: async () =>
                    {
                        // Re-check cache in case another instance populated it
                        var cached = await Database.StringGetAsync(cacheKey);
                        if (cached.HasValue)
                        {
                            var jsonStr = (string?)cached;
                            if (jsonStr is not null)
                            {
                                return JsonSerializer.Deserialize<CachedProvider>(jsonStr, JsonOptions);
                            }
                        }
                        return null;
                    },
                    factory: () => databaseFallback(providerId));

                if (dbCredential != null)
                {
                    await SetProviderAsync(providerId, dbCredential);
                    return dbCredential;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error accessing Provider Credential cache, falling back to database: {ProviderId}", providerId);
                await TrackMissAsync(ServiceName);
                return await databaseFallback(providerId);
            }
        }

        /// <summary>
        /// Get Provider Credential by name from cache with database fallback
        /// </summary>
        public async Task<CachedProvider?> GetProviderByNameAsync(
            string providerName,
            Func<string, Task<CachedProvider?>> databaseFallback)
        {
            // Always go to database for name lookups since names can change
            try
            {
                Logger.LogDebug("Provider credential lookup by name, querying database: {ProviderName}", providerName);
                await TrackMissAsync(ServiceName);

                var dbCredential = await databaseFallback(providerName);

                if (dbCredential != null)
                {
                    // Cache by ID only
                    await SetProviderAsync(dbCredential.Provider.Id, dbCredential);
                    return dbCredential;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error accessing Provider Credential by name, falling back to database: {ProviderName}", providerName);
                return await databaseFallback(providerName);
            }
        }

        /// <summary>
        /// Invalidate a Provider Credential in cache
        /// </summary>
        public async Task InvalidateProviderAsync(int providerId)
        {
            try
            {
                var cacheKey = CacheKeys.Provider.ById(providerId);

                // Get the provider to find its name for name-based key invalidation
                var cachedValue = await Database.StringGetAsync(cacheKey);
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var credential = JsonSerializer.Deserialize<Provider>(jsonString, JsonOptions);
                        if (credential != null)
                        {
                            // No longer using name-based keys
                        }
                    }
                }

                // Delete ID-based key
                await Database.KeyDeleteAsync(cacheKey);
                await TrackInvalidationAsync(ServiceName);

                Logger.LogDebug("Provider credential cache invalidated: {ProviderId}", providerId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating Provider Credential cache: {ProviderId}", providerId);
            }
        }

        /// <summary>
        /// Invalidate a Provider Credential by name in cache
        /// </summary>
        public Task InvalidateProviderByNameAsync(string providerName)
        {
            // Since we don't cache by name anymore, this is a no-op
            Logger.LogDebug("InvalidateProviderByNameAsync called but we don't cache by name. Provider: {ProviderName}", providerName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Clear all Provider Credential entries from cache
        /// </summary>
        public async Task ClearAllProvidersAsync()
        {
            try
            {
                await ClearAllByPatternAsync(CacheKeys.Provider.Prefix + "*");

                // Clean up any legacy name-based keys
                await ClearAllByPatternAsync(CacheKeys.Provider.NamePrefix + "*");

                Logger.LogWarning("All provider credential cache entries cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing all provider credential cache entries");
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<ProviderCacheStats> GetStatsAsync()
        {
            try
            {
                var (hits, misses, invalidations, resetTime) = await GetBaseStatsAsync(ServiceName);

                return new ProviderCacheStats
                {
                    HitCount = hits,
                    MissCount = misses,
                    InvalidationCount = invalidations,
                    LastResetTime = resetTime,
                    EntryCount = CountEntries(CacheKeys.Provider.Prefix + "*")
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting provider credential cache statistics");
                return new ProviderCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task SetProviderAsync(int providerId, CachedProvider credential)
        {
            var cacheKey = CacheKeys.Provider.ById(providerId);
            await SetCacheEntryAsync(cacheKey, credential);

            Logger.LogDebug("Provider credential cached: {ProviderId} with {KeyCount} keys",
                providerId, credential.Keys.Count);
        }
    }
}
