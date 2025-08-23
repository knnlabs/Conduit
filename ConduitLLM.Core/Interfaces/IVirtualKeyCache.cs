using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Cache interface for Virtual Key operations with immediate invalidation
    /// </summary>
    public interface IVirtualKeyCache
    {
        /// <summary>
        /// Get Virtual Key from cache with database fallback
        /// </summary>
        /// <param name="keyHash">The hashed key to look up</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Virtual Key entity or null if not found</returns>
        Task<VirtualKey?> GetVirtualKeyAsync(string keyHash, Func<string, Task<VirtualKey?>> databaseFallback);

        /// <summary>
        /// Immediately invalidate a Virtual Key in cache and notify all instances
        /// SECURITY CRITICAL: Used when keys are disabled/compromised
        /// </summary>
        /// <param name="keyHash">The hashed key to invalidate</param>
        Task InvalidateVirtualKeyAsync(string keyHash);

        /// <summary>
        /// Bulk invalidate multiple Virtual Keys efficiently
        /// </summary>
        /// <param name="keyHashes">Array of hashed keys to invalidate</param>
        Task InvalidateVirtualKeysAsync(string[] keyHashes);

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        Task<VirtualKeyCacheStats> GetStatsAsync();
    }

    /// <summary>
    /// Cache performance statistics
    /// </summary>
    public class VirtualKeyCacheStats
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long InvalidationCount { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public TimeSpan AverageGetTime { get; set; }
        public DateTime LastResetTime { get; set; }
    }
}