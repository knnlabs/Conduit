using System;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Provider Credential cache with event-driven invalidation
    /// </summary>
    public class RedisProviderCredentialCache : IProviderCredentialCache
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisProviderCredentialCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);
        private const string KeyPrefix = "provider:";
        private const string NameKeyPrefix = "provider:name:";
        
        // Statistics tracking keys
        private const string STATS_HIT_KEY = "conduit:cache:provider:stats:hits";
        private const string STATS_MISS_KEY = "conduit:cache:provider:stats:misses";
        private const string STATS_INVALIDATION_KEY = "conduit:cache:provider:stats:invalidations";
        private const string STATS_RESET_TIME_KEY = "conduit:cache:provider:stats:reset_time";

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisProviderCredentialCache(
            IConnectionMultiplexer redis,
            ILogger<RedisProviderCredentialCache> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            
            // Initialize stats reset time if not exists
            _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.ToString("O"), when: When.NotExists).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get Provider Credential from cache with database fallback
        /// </summary>
        public async Task<CachedProviderCredential?> GetProviderCredentialAsync(
            int providerId, 
            Func<int, Task<CachedProviderCredential?>> databaseFallback)
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
                        var credential = JsonSerializer.Deserialize<CachedProviderCredential>(jsonString, _jsonOptions);
                        
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
                    await SetProviderCredentialAsync(providerId, dbCredential);
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
        public async Task<CachedProviderCredential?> GetProviderCredentialByNameAsync(
            string providerName, 
            Func<string, Task<CachedProviderCredential?>> databaseFallback)
        {
            var cacheKey = NameKeyPrefix + providerName.ToLowerInvariant();
            
            try
            {
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var credential = JsonSerializer.Deserialize<CachedProviderCredential>(jsonString, _jsonOptions);
                        
                        if (credential != null)
                        {
                            _logger.LogDebug("Provider credential cache hit by name: {ProviderName}", providerName);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return credential;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Provider credential cache miss by name, querying database: {ProviderName}", providerName);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbCredential = await databaseFallback(providerName);
                
                if (dbCredential != null)
                {
                    // Cache by both ID and name
                    await SetProviderCredentialAsync(dbCredential.Provider.Id, dbCredential);
                    return dbCredential;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Provider Credential cache by name, falling back to database: {ProviderName}", providerName);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
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
                        var credential = JsonSerializer.Deserialize<ProviderCredential>(jsonString, _jsonOptions);
                        if (credential != null)
                        {
                            // Delete name-based key too
                            var nameKey = NameKeyPrefix + credential.ProviderName.ToLowerInvariant();
                            await _database.KeyDeleteAsync(nameKey);
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
        public async Task InvalidateProviderByNameAsync(string providerName)
        {
            try
            {
                var nameKey = NameKeyPrefix + providerName.ToLowerInvariant();
                
                // Get the provider to find its ID for ID-based key invalidation
                var cachedValue = await _database.StringGetAsync(nameKey);
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var credential = JsonSerializer.Deserialize<CachedProviderCredential>(jsonString, _jsonOptions);
                        if (credential != null)
                        {
                            // Delete ID-based key too
                            var idKey = KeyPrefix + credential.Provider.Id;
                            await _database.KeyDeleteAsync(idKey);
                        }
                    }
                }
                
                // Delete name-based key
                await _database.KeyDeleteAsync(nameKey);
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                
                _logger.LogInformation("Provider credential cache invalidated by name: {ProviderName}", providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Provider Credential cache by name: {ProviderName}", providerName);
            }
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
        public async Task<ProviderCredentialCacheStats> GetStatsAsync()
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
                
                return new ProviderCredentialCacheStats
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
                return new ProviderCredentialCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task SetProviderCredentialAsync(int providerId, CachedProviderCredential credential)
        {
            var cacheKey = KeyPrefix + providerId;
            var nameKey = NameKeyPrefix + credential.Provider.ProviderName.ToLowerInvariant();
            var serialized = JsonSerializer.Serialize(credential, _jsonOptions);
            
            // Cache by both ID and name with same expiry
            await _database.StringSetAsync(cacheKey, serialized, _defaultExpiry);
            await _database.StringSetAsync(nameKey, serialized, _defaultExpiry);
            
            _logger.LogDebug("Provider credential cached: {ProviderId} / {ProviderName} with {KeyCount} keys", 
                providerId, credential.Provider.ProviderName, credential.Keys.Count);
        }
    }
}