using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based Provider Credential cache with event-driven invalidation
    /// </summary>
    public class RedisProviderCache : IProviderCache
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisProviderCache> _logger;
        private readonly IDistributedCachePopulator _cachePopulator;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisProviderCache(
            IConnectionMultiplexer redis,
            ILogger<RedisProviderCache> logger,
            IDistributedCachePopulator cachePopulator)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            _cachePopulator = cachePopulator;

            // Initialize stats reset time if not exists (fire-and-forget, non-blocking)
            _ = _database.StringSetAsync(CacheKeys.Stats.ResetTime(CacheKeys.Stats.ProviderService), DateTime.UtcNow.ToString("O"), when: When.NotExists)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogWarning(t.Exception, "Failed to initialize stats reset time");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
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
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var credential = JsonSerializer.Deserialize<CachedProvider>(jsonString, _jsonOptions);
                        
                        if (credential != null)
                        {
                            _logger.LogDebug("Provider credential cache hit: {ProviderId}", providerId);
                            await _database.StringIncrementAsync(CacheKeys.Stats.Hits(CacheKeys.Stats.ProviderService));
                            return credential;
                        }
                    }
                }
                
                // Cache miss - use stampede prevention to avoid multiple concurrent DB queries
                _logger.LogDebug("Provider credential cache miss, querying database: {ProviderId}", providerId);
                await _database.StringIncrementAsync(CacheKeys.Stats.Misses(CacheKeys.Stats.ProviderService));

                var dbCredential = await _cachePopulator.GetOrPopulateAsync(
                    lockKey: $"populate:provider:{providerId}",
                    cacheCheck: async () =>
                    {
                        // Re-check cache in case another instance populated it
                        var cached = await _database.StringGetAsync(cacheKey);
                        if (cached.HasValue)
                        {
                            var jsonStr = (string?)cached;
                            if (jsonStr is not null)
                            {
                                return JsonSerializer.Deserialize<CachedProvider>(jsonStr, _jsonOptions);
                            }
                        }
                        return null;
                    },
                    factory: () => databaseFallback(providerId));

                if (dbCredential != null)
                {
                    // Cache the credential
                    await SetProviderAsync(providerId, dbCredential);
                    return dbCredential;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Provider Credential cache, falling back to database: {ProviderId}", providerId);
                await _database.StringIncrementAsync(CacheKeys.Stats.Misses(CacheKeys.Stats.ProviderService));
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
            // We cannot cache by name as it's mutable
            try
            {
                _logger.LogDebug("Provider credential lookup by name, querying database: {ProviderName}", providerName);
                await _database.StringIncrementAsync(CacheKeys.Stats.Misses(CacheKeys.Stats.ProviderService));
                
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
                _logger.LogError(ex, "Error accessing Provider Credential by name, falling back to database: {ProviderName}", providerName);
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
                var cachedValue = await _database.StringGetAsync(cacheKey);
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var credential = JsonSerializer.Deserialize<Provider>(jsonString, _jsonOptions);
                        if (credential != null)
                        {
                            // No longer using name-based keys
                        }
                    }
                }
                
                // Delete ID-based key
                await _database.KeyDeleteAsync(cacheKey);
                await _database.StringIncrementAsync(CacheKeys.Stats.Invalidations(CacheKeys.Stats.ProviderService));
                
                _logger.LogInformation("Provider credential cache invalidated: {ProviderId}", providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Provider Credential cache: {ProviderId}", providerId);
            }
        }

        /// <summary>
        /// Invalidate a Provider Credential by name in cache
        /// </summary>
        public Task InvalidateProviderByNameAsync(string providerName)
        {
            // Since we don't cache by name anymore, this is a no-op
            // We would need the provider ID to invalidate the cache
            _logger.LogWarning("InvalidateProviderByNameAsync called but we don't cache by name. Provider: {ProviderName}", providerName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Clear all Provider Credential entries from cache
        /// </summary>
        public async Task ClearAllProvidersAsync()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: CacheKeys.Provider.Prefix + "*");
                
                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                // Clean up any legacy name-based keys
                var nameKeys = server.Keys(pattern: CacheKeys.Provider.NamePrefix + "*");
                foreach (var key in nameKeys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                _logger.LogWarning("All provider credential cache entries cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all provider credential cache entries");
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<ProviderCacheStats> GetStatsAsync()
        {
            try
            {
                var hits = await _database.StringGetAsync(CacheKeys.Stats.Hits(CacheKeys.Stats.ProviderService));
                var misses = await _database.StringGetAsync(CacheKeys.Stats.Misses(CacheKeys.Stats.ProviderService));
                var invalidations = await _database.StringGetAsync(CacheKeys.Stats.Invalidations(CacheKeys.Stats.ProviderService));
                var resetTime = await _database.StringGetAsync(CacheKeys.Stats.ResetTime(CacheKeys.Stats.ProviderService));
                
                // Count entries
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: CacheKeys.Provider.Prefix + "*");
                var entryCount = 0L;
                foreach (var _ in keys)
                {
                    entryCount++;
                }
                
                return new ProviderCacheStats
                {
                    HitCount = hits.HasValue ? (long)hits : 0,
                    MissCount = misses.HasValue ? (long)misses : 0,
                    InvalidationCount = invalidations.HasValue ? (long)invalidations : 0,
                    LastResetTime = resetTime.HasValue && DateTime.TryParse(resetTime, out var time) ? time : DateTime.UtcNow,
                    EntryCount = entryCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential cache statistics");
                return new ProviderCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task SetProviderAsync(int providerId, CachedProvider credential)
        {
            var cacheKey = CacheKeys.Provider.ById(providerId);
            var serialized = JsonSerializer.Serialize(credential, _jsonOptions);
            
            // Cache by ID only - never by name since names can change
            await _database.StringSetAsync(cacheKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Provider credential cached: {ProviderId} with {KeyCount} keys", 
                providerId, credential.Keys.Count);
        }
    }
}