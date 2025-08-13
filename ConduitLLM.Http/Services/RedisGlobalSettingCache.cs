using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Global Setting cache with event-driven invalidation
    /// </summary>
    public class RedisGlobalSettingCache : IGlobalSettingCache
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisGlobalSettingCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(2);
        private readonly TimeSpan _authKeyExpiry = TimeSpan.FromMinutes(15); // Shorter expiry for auth keys
        private const string KeyPrefix = "globalsetting:";
        private const string AuthKeyCache = "globalsetting:authkey";
        
        // Statistics tracking keys
        private const string STATS_HIT_KEY = "conduit:cache:globalsetting:stats:hits";
        private const string STATS_MISS_KEY = "conduit:cache:globalsetting:stats:misses";
        private const string STATS_INVALIDATION_KEY = "conduit:cache:globalsetting:stats:invalidations";
        private const string STATS_RESET_TIME_KEY = "conduit:cache:globalsetting:stats:reset_time";
        private const string STATS_AUTH_HIT_KEY = "conduit:cache:globalsetting:stats:auth_hits";
        private const string STATS_AUTH_MISS_KEY = "conduit:cache:globalsetting:stats:auth_misses";

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisGlobalSettingCache(
            IConnectionMultiplexer redis,
            ILogger<RedisGlobalSettingCache> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            
            // Initialize stats reset time if not exists
            _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.ToString("O"), when: When.NotExists).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get Global Setting from cache with database fallback
        /// </summary>
        public async Task<GlobalSetting?> GetSettingAsync(
            string settingKey, 
            Func<string, Task<GlobalSetting?>> databaseFallback)
        {
            var cacheKey = KeyPrefix + settingKey.ToLowerInvariant();
            
            try
            {
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var setting = JsonSerializer.Deserialize<GlobalSetting>(jsonString, _jsonOptions);
                        
                        if (setting != null)
                        {
                            _logger.LogDebug("Global setting cache hit: {SettingKey}", settingKey);
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return setting;
                        }
                    }
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Global setting cache miss, querying database: {SettingKey}", settingKey);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                
                var dbSetting = await databaseFallback(settingKey);
                
                if (dbSetting != null)
                {
                    // Cache the setting
                    await SetSettingAsync(dbSetting);
                    return dbSetting;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Global Setting cache, falling back to database: {SettingKey}", settingKey);
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                return await databaseFallback(settingKey);
            }
        }

        /// <summary>
        /// Get multiple Global Settings from cache with database fallback
        /// </summary>
        public async Task<Dictionary<string, GlobalSetting>> GetSettingsAsync(
            string[] settingKeys, 
            Func<string[], Task<List<GlobalSetting>>> databaseFallback)
        {
            var result = new Dictionary<string, GlobalSetting>();
            var missingKeys = new List<string>();
            
            try
            {
                // Try to get all settings from cache
                foreach (var key in settingKeys)
                {
                    var cacheKey = KeyPrefix + key.ToLowerInvariant();
                    var cachedValue = await _database.StringGetAsync(cacheKey);
                    
                    if (cachedValue.HasValue)
                    {
                        var jsonString = (string?)cachedValue;
                        if (jsonString is not null)
                        {
                            var setting = JsonSerializer.Deserialize<GlobalSetting>(jsonString, _jsonOptions);
                            if (setting != null)
                            {
                                result[key] = setting;
                                await _database.StringIncrementAsync(STATS_HIT_KEY);
                                continue;
                            }
                        }
                    }
                    
                    missingKeys.Add(key);
                    await _database.StringIncrementAsync(STATS_MISS_KEY);
                }
                
                // Fetch missing settings from database
                if (missingKeys.Count() > 0)
                {
                    _logger.LogDebug("Global settings cache miss for {Count} keys, querying database", missingKeys.Count);
                    var dbSettings = await databaseFallback(missingKeys.ToArray());
                    
                    foreach (var setting in dbSettings)
                    {
                        result[setting.Key] = setting;
                        await SetSettingAsync(setting);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Global Settings cache, falling back to database");
                var dbSettings = await databaseFallback(settingKeys);
                return dbSettings.ToDictionary(s => s.Key, s => s);
            }
        }

        /// <summary>
        /// Get authentication key from cache with specialized handling
        /// </summary>
        public async Task<string?> GetAuthenticationKeyAsync(Func<Task<string?>> databaseFallback)
        {
            try
            {
                var cachedValue = await _database.StringGetAsync(AuthKeyCache);
                
                if (cachedValue.HasValue)
                {
                    _logger.LogDebug("Authentication key cache hit");
                    await _database.StringIncrementAsync(STATS_AUTH_HIT_KEY);
                    return (string?)cachedValue;
                }
                
                // Cache miss - fallback to database
                _logger.LogDebug("Authentication key cache miss, querying database");
                await _database.StringIncrementAsync(STATS_AUTH_MISS_KEY);
                
                var authKey = await databaseFallback();
                
                if (!string.IsNullOrEmpty(authKey))
                {
                    // Cache with shorter expiry for auth keys
                    await _database.StringSetAsync(AuthKeyCache, authKey, _authKeyExpiry);
                    return authKey;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing authentication key cache, falling back to database");
                await _database.StringIncrementAsync(STATS_AUTH_MISS_KEY);
                return await databaseFallback();
            }
        }

        /// <summary>
        /// Invalidate a Global Setting in cache
        /// </summary>
        public async Task InvalidateSettingAsync(string settingKey)
        {
            try
            {
                var cacheKey = KeyPrefix + settingKey.ToLowerInvariant();
                await _database.KeyDeleteAsync(cacheKey);
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                
                // If it's the auth key, invalidate the specialized cache too
                if (settingKey.Equals("AuthenticationKey", StringComparison.OrdinalIgnoreCase))
                {
                    await _database.KeyDeleteAsync(AuthKeyCache);
                }
                
                _logger.LogInformation("Global setting cache invalidated: {SettingKey}", settingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Global Setting cache: {SettingKey}", settingKey);
            }
        }

        /// <summary>
        /// Invalidate multiple Global Settings in cache
        /// </summary>
        public async Task InvalidateSettingsAsync(string[] settingKeys)
        {
            try
            {
                var cacheKeys = settingKeys.Select(k => (RedisKey)(KeyPrefix + k.ToLowerInvariant())).ToArray();
                await _database.KeyDeleteAsync(cacheKeys);
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY, settingKeys.Length);
                
                // Check if auth key is in the list
                if (settingKeys.Any(k => k.Equals("AuthenticationKey", StringComparison.OrdinalIgnoreCase)))
                {
                    await _database.KeyDeleteAsync(AuthKeyCache);
                }
                
                _logger.LogInformation("Global settings cache invalidated: {Count} keys", settingKeys.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating multiple Global Settings cache");
            }
        }

        /// <summary>
        /// Invalidate all authentication-related settings
        /// </summary>
        public async Task InvalidateAuthenticationSettingsAsync()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var authKeys = server.Keys(pattern: KeyPrefix + "auth*");
                
                foreach (var key in authKeys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                // Also invalidate the specialized auth key cache
                await _database.KeyDeleteAsync(AuthKeyCache);
                
                _logger.LogWarning("All authentication-related settings cache entries cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating authentication settings cache");
            }
        }

        /// <summary>
        /// Clear all Global Setting entries from cache
        /// </summary>
        public async Task ClearAllSettingsAsync()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: KeyPrefix + "*");
                
                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                // Also clear the auth key cache
                await _database.KeyDeleteAsync(AuthKeyCache);
                
                _logger.LogWarning("All global setting cache entries cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all global setting cache entries");
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<GlobalSettingCacheStats> GetStatsAsync()
        {
            try
            {
                var hits = await _database.StringGetAsync(STATS_HIT_KEY);
                var misses = await _database.StringGetAsync(STATS_MISS_KEY);
                var invalidations = await _database.StringGetAsync(STATS_INVALIDATION_KEY);
                var authHits = await _database.StringGetAsync(STATS_AUTH_HIT_KEY);
                var authMisses = await _database.StringGetAsync(STATS_AUTH_MISS_KEY);
                var resetTime = await _database.StringGetAsync(STATS_RESET_TIME_KEY);
                
                // Count entries
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: KeyPrefix + "*");
                var entryCount = 0L;
                foreach (var _ in keys)
                {
                    entryCount++;
                }
                
                return new GlobalSettingCacheStats
                {
                    HitCount = hits.HasValue ? (long)hits : 0,
                    MissCount = misses.HasValue ? (long)misses : 0,
                    InvalidationCount = invalidations.HasValue ? (long)invalidations : 0,
                    AuthKeyHits = authHits.HasValue ? (long)authHits : 0,
                    AuthKeyMisses = authMisses.HasValue ? (long)authMisses : 0,
                    LastResetTime = resetTime.HasValue && DateTime.TryParse(resetTime, out var time) ? time : DateTime.UtcNow,
                    EntryCount = entryCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global setting cache statistics");
                return new GlobalSettingCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private async Task SetSettingAsync(GlobalSetting setting)
        {
            var cacheKey = KeyPrefix + setting.Key.ToLowerInvariant();
            var serialized = JsonSerializer.Serialize(setting, _jsonOptions);
            
            // Use shorter expiry for auth-related settings
            var expiry = setting.Key.StartsWith("Auth", StringComparison.OrdinalIgnoreCase) 
                ? _authKeyExpiry 
                : _defaultExpiry;
            
            await _database.StringSetAsync(cacheKey, serialized, expiry);
            
            _logger.LogDebug("Global setting cached: {SettingKey}", setting.Key);
        }
    }
}