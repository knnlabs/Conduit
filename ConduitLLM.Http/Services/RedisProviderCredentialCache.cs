using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Provider Credential cache with event-driven invalidation
    /// </summary>
    public class RedisProviderCache : IProviderCache
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisProviderCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);
        private const string KeyPrefix = "provider:";
        private const string NameKeyPrefix = "provider:name:"; // DEPRECATED - only for cleanup
        
        // Statistics tracking keys
        private const string STATS_HIT_KEY = "conduit:cache:provider:stats:hits";
        private const string STATS_MISS_KEY = "conduit:cache:provider:stats:misses";
        private const string STATS_INVALIDATION_KEY = "conduit:cache:provider:stats:invalidations";
        private const string STATS_RESET_TIME_KEY = "conduit:cache:provider:stats:reset_time";

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisProviderCache(
            IConnectionMultiplexer redis,
            ILogger<RedisProviderCache> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            
            // Initialize stats reset time if not exists
            _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.ToString("O"), when: When.NotExists).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get Provider Credential from cache with database fallback
        /// </summary>
        public async Task<CachedProvider?> GetProviderAsync(
            int providerId, 
            Func<int, Task<CachedProvider?>> databaseFallback)
        {
            var cacheKey = KeyPrefix + providerId;
            
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
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return credential;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Provider credential cache miss, querying database: {ProviderId}", providerId);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbCredential = await databaseFallback(providerId);
                
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
                await _database.StringIncrementAsync(STATS_MISS_KEY);
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
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
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
                var cacheKey = KeyPrefix + providerId;
                
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
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                
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
                var keys = server.Keys(pattern: KeyPrefix + "*");
                
                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                // Clean up any legacy name-based keys
                var nameKeys = server.Keys(pattern: NameKeyPrefix + "*");
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
                var hits = await _database.StringGetAsync(STATS_HIT_KEY);
                var misses = await _database.StringGetAsync(STATS_MISS_KEY);
                var invalidations = await _database.StringGetAsync(STATS_INVALIDATION_KEY);
                var resetTime = await _database.StringGetAsync(STATS_RESET_TIME_KEY);
                
                // Count entries
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: KeyPrefix + "*");
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
            var cacheKey = KeyPrefix + providerId;
            var serialized = JsonSerializer.Serialize(credential, _jsonOptions);
            
            // Cache by ID only - never by name since names can change
            await _database.StringSetAsync(cacheKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Provider credential cached: {ProviderId} with {KeyCount} keys", 
                providerId, credential.Keys.Count);
        }
    }
}