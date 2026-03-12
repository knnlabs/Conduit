using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based Global Setting cache with event-driven invalidation
    /// </summary>
    public class RedisGlobalSettingCache : RedisCacheServiceBase, IGlobalSettingCache
    {
        private readonly TimeSpan _authKeyExpiry = TimeSpan.FromMinutes(15); // Shorter expiry for auth keys

        private static readonly string ServiceName = CacheKeys.Stats.GlobalSettingService;

        public RedisGlobalSettingCache(
            IConnectionMultiplexer redis,
            ILogger<RedisGlobalSettingCache> logger)
            : base(redis, logger, TimeSpan.FromHours(2))
        {
            InitializeStatsResetTime(ServiceName);
        }

        /// <summary>
        /// Get Global Setting from cache with database fallback
        /// </summary>
        public async Task<GlobalSetting?> GetSettingAsync(
            string settingKey,
            Func<string, Task<GlobalSetting?>> databaseFallback)
        {
            var cacheKey = CacheKeys.GlobalSetting.Prefix + settingKey.ToLowerInvariant();

            return await GetOrFallbackAsync<GlobalSetting>(
                cacheKey,
                ServiceName,
                () => databaseFallback(settingKey),
                expiry: GetExpiryForKey(settingKey),
                debugLabel: $"Global setting: {settingKey}");
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
                    var cacheKey = CacheKeys.GlobalSetting.Prefix + key.ToLowerInvariant();
                    var cachedValue = await Database.StringGetAsync(cacheKey);

                    if (cachedValue.HasValue)
                    {
                        var jsonString = (string?)cachedValue;
                        if (jsonString is not null)
                        {
                            var setting = JsonSerializer.Deserialize<GlobalSetting>(jsonString, JsonOptions);
                            if (setting != null)
                            {
                                result[key] = setting;
                                await TrackHitAsync(ServiceName);
                                continue;
                            }
                        }
                    }

                    missingKeys.Add(key);
                    await TrackMissAsync(ServiceName);
                }

                // Fetch missing settings from database
                if (missingKeys.Any())
                {
                    Logger.LogDebug("Global settings cache miss for {Count} keys, querying database", missingKeys.Count);
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
                Logger.LogError(ex, "Error accessing Global Settings cache, falling back to database");
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
                var cachedValue = await Database.StringGetAsync(CacheKeys.GlobalSetting.AuthKey);

                if (cachedValue.HasValue)
                {
                    Logger.LogDebug("Authentication key cache hit");
                    await Database.StringIncrementAsync(CacheKeys.Stats.AuthHits());
                    return (string?)cachedValue;
                }

                // Cache miss - fallback to database
                Logger.LogDebug("Authentication key cache miss, querying database");
                await Database.StringIncrementAsync(CacheKeys.Stats.AuthMisses());

                var authKey = await databaseFallback();

                if (!string.IsNullOrEmpty(authKey))
                {
                    await Database.StringSetAsync(CacheKeys.GlobalSetting.AuthKey, authKey, _authKeyExpiry);
                    return authKey;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error accessing authentication key cache, falling back to database");
                await Database.StringIncrementAsync(CacheKeys.Stats.AuthMisses());
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
                var cacheKey = CacheKeys.GlobalSetting.Prefix + settingKey.ToLowerInvariant();
                await Database.KeyDeleteAsync(cacheKey);
                await TrackInvalidationAsync(ServiceName);

                // If it's the auth key, invalidate the specialized cache too
                if (settingKey.Equals("AuthenticationKey", StringComparison.OrdinalIgnoreCase))
                {
                    await Database.KeyDeleteAsync(CacheKeys.GlobalSetting.AuthKey);
                }

                Logger.LogInformation("Global setting cache invalidated: {SettingKey}", settingKey);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating Global Setting cache: {SettingKey}", settingKey);
            }
        }

        /// <summary>
        /// Invalidate multiple Global Settings in cache
        /// </summary>
        public async Task InvalidateSettingsAsync(string[] settingKeys)
        {
            try
            {
                var cacheKeys = settingKeys.Select(k => (RedisKey)(CacheKeys.GlobalSetting.Prefix + k.ToLowerInvariant())).ToArray();
                await Database.KeyDeleteAsync(cacheKeys);
                await TrackInvalidationAsync(ServiceName, settingKeys.Length);

                // Check if auth key is in the list
                if (settingKeys.Any(k => k.Equals("AuthenticationKey", StringComparison.OrdinalIgnoreCase)))
                {
                    await Database.KeyDeleteAsync(CacheKeys.GlobalSetting.AuthKey);
                }

                Logger.LogInformation("Global settings cache invalidated: {Count} keys", settingKeys.Length);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating multiple Global Settings cache");
            }
        }

        /// <summary>
        /// Invalidate all authentication-related settings
        /// </summary>
        public async Task InvalidateAuthenticationSettingsAsync()
        {
            try
            {
                await ClearAllByPatternAsync(CacheKeys.GlobalSetting.Prefix + "auth*");
                await Database.KeyDeleteAsync(CacheKeys.GlobalSetting.AuthKey);

                Logger.LogWarning("All authentication-related settings cache entries cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error invalidating authentication settings cache");
            }
        }

        /// <summary>
        /// Clear all Global Setting entries from cache
        /// </summary>
        public async Task ClearAllSettingsAsync()
        {
            try
            {
                await ClearAllByPatternAsync(CacheKeys.GlobalSetting.Prefix + "*");
                await Database.KeyDeleteAsync(CacheKeys.GlobalSetting.AuthKey);

                Logger.LogWarning("All global setting cache entries cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing all global setting cache entries");
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public async Task<GlobalSettingCacheStats> GetStatsAsync()
        {
            try
            {
                var (hits, misses, invalidations, resetTime) = await GetBaseStatsAsync(ServiceName);
                var authHits = await Database.StringGetAsync(CacheKeys.Stats.AuthHits());
                var authMisses = await Database.StringGetAsync(CacheKeys.Stats.AuthMisses());

                return new GlobalSettingCacheStats
                {
                    HitCount = hits,
                    MissCount = misses,
                    InvalidationCount = invalidations,
                    AuthKeyHits = authHits.HasValue ? (long)authHits : 0,
                    AuthKeyMisses = authMisses.HasValue ? (long)authMisses : 0,
                    LastResetTime = resetTime,
                    EntryCount = CountEntries(CacheKeys.GlobalSetting.Prefix + "*")
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting global setting cache statistics");
                return new GlobalSettingCacheStats { LastResetTime = DateTime.UtcNow };
            }
        }

        private TimeSpan GetExpiryForKey(string settingKey)
        {
            return settingKey.StartsWith("Auth", StringComparison.OrdinalIgnoreCase)
                ? _authKeyExpiry
                : DefaultExpiry;
        }

        private async Task SetSettingAsync(GlobalSetting setting)
        {
            var cacheKey = CacheKeys.GlobalSetting.Prefix + setting.Key.ToLowerInvariant();
            await SetCacheEntryAsync(cacheKey, setting, GetExpiryForKey(setting.Key));
            Logger.LogDebug("Global setting cached: {SettingKey}", setting.Key);
        }
    }
}
