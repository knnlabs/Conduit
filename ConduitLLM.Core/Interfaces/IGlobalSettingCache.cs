using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Cache interface for Global Setting operations with event-driven invalidation
    /// </summary>
    public interface IGlobalSettingCache
    {
        /// <summary>
        /// Get Global Setting from cache with database fallback
        /// </summary>
        /// <param name="settingKey">The setting key to look up</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Global Setting entity or null if not found</returns>
        Task<GlobalSetting?> GetSettingAsync(string settingKey, Func<string, Task<GlobalSetting?>> databaseFallback);

        /// <summary>
        /// Get multiple Global Settings from cache with database fallback
        /// </summary>
        /// <param name="settingKeys">The setting keys to look up</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Dictionary of setting key to Global Setting entity</returns>
        Task<Dictionary<string, GlobalSetting>> GetSettingsAsync(string[] settingKeys, Func<string[], Task<List<GlobalSetting>>> databaseFallback);

        /// <summary>
        /// Get authentication key from cache
        /// This is a specialized method for frequently accessed auth keys
        /// </summary>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Authentication key value or null if not found</returns>
        Task<string?> GetAuthenticationKeyAsync(Func<Task<string?>> databaseFallback);

        /// <summary>
        /// Invalidate a Global Setting in cache
        /// </summary>
        /// <param name="settingKey">The setting key to invalidate</param>
        Task InvalidateSettingAsync(string settingKey);

        /// <summary>
        /// Invalidate multiple Global Settings in cache
        /// </summary>
        /// <param name="settingKeys">The setting keys to invalidate</param>
        Task InvalidateSettingsAsync(string[] settingKeys);

        /// <summary>
        /// Invalidate all authentication-related settings
        /// Used when auth configuration changes
        /// </summary>
        Task InvalidateAuthenticationSettingsAsync();

        /// <summary>
        /// Clear all Global Setting entries from cache
        /// Used when bulk changes occur or during system reinitialization
        /// </summary>
        Task ClearAllSettingsAsync();

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        Task<GlobalSettingCacheStats> GetStatsAsync();
    }

    /// <summary>
    /// Cache performance statistics for Global Settings
    /// </summary>
    public class GlobalSettingCacheStats
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long InvalidationCount { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public TimeSpan AverageGetTime { get; set; }
        public DateTime LastResetTime { get; set; }
        public long EntryCount { get; set; }
        public long AuthKeyHits { get; set; }
        public long AuthKeyMisses { get; set; }
    }
}