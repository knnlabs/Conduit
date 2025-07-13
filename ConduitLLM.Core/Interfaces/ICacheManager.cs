using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Unified cache management interface that provides consistent operations across
    /// different cache implementations (memory, distributed) with region support and statistics.
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// Gets a value from the cache.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The cached value or default if not found.</returns>
        Task<T?> GetAsync<T>(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value from the cache with detailed entry information.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The cache entry with metadata or null if not found.</returns>
        Task<CacheEntry<T>?> GetEntryAsync<T>(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="ttl">Time-to-live for the entry. If null, uses region default.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetAsync<T>(string key, T value, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache with additional options.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="entry">The cache entry with value and metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetEntryAsync<T>(CacheEntry<T> entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value from the cache or creates it if not found.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="factory">Factory function to create the value if not cached.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="ttl">Time-to-live for the entry. If null, uses region default.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The cached or newly created value.</returns>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a specific entry from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the entry was removed, false if not found.</returns>
        Task<bool> RemoveAsync(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes multiple entries from the cache.
        /// </summary>
        /// <param name="keys">The cache keys to remove.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of entries removed.</returns>
        Task<int> RemoveManyAsync(IEnumerable<string> keys, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes entries matching a pattern from the cache.
        /// </summary>
        /// <param name="pattern">The pattern to match (supports wildcards: * and ?).</param>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of entries removed.</returns>
        Task<int> RemoveByPatternAsync(string pattern, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all entries in a specific region.
        /// </summary>
        /// <param name="region">The cache region to clear.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ClearRegionAsync(CacheRegion region, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cache entries across all regions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ClearAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the key exists, false otherwise.</returns>
        Task<bool> ExistsAsync(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics for a specific cache region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics for the region.</returns>
        Task<CacheRegionStatistics> GetRegionStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics for all cache regions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics for all regions.</returns>
        Task<Dictionary<CacheRegion, CacheRegionStatistics>> GetAllStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the configuration for a specific region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <returns>Configuration for the region.</returns>
        CacheRegionConfig GetRegionConfig(CacheRegion region);

        /// <summary>
        /// Updates the configuration for a specific region.
        /// </summary>
        /// <param name="config">The new configuration.</param>
        Task UpdateRegionConfigAsync(CacheRegionConfig config);

        /// <summary>
        /// Lists all keys in a specific region (use with caution in production).
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="pattern">Optional pattern to filter keys.</param>
        /// <param name="maxCount">Maximum number of keys to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of cache keys.</returns>
        Task<IEnumerable<string>> ListKeysAsync(CacheRegion region, string? pattern = null, int maxCount = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets entries in a specific region with pagination support.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="skip">Number of entries to skip.</param>
        /// <param name="take">Number of entries to take.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of cache entries.</returns>
        Task<IEnumerable<CacheEntry<object>>> GetEntriesAsync(CacheRegion region, int skip = 0, int take = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes the TTL for an existing entry without modifying its value.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="region">The cache region.</param>
        /// <param name="ttl">New TTL. If null, uses region default.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the entry was refreshed, false if not found.</returns>
        Task<bool> RefreshAsync(string key, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when a cache entry is evicted.
        /// </summary>
        event EventHandler<CacheEvictionEventArgs>? EntryEvicted;

        /// <summary>
        /// Event raised when cache statistics are updated.
        /// </summary>
        event EventHandler<CacheStatisticsEventArgs>? StatisticsUpdated;

        /// <summary>
        /// Gets whether the cache manager is available and operational.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the health status of the cache system.
        /// </summary>
        Task<CacheHealthStatus> GetHealthStatusAsync();
    }

    /// <summary>
    /// Statistics for a cache region.
    /// </summary>
    public class CacheRegionStatistics
    {
        public CacheRegion Region { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long SetCount { get; set; }
        public long EvictionCount { get; set; }
        public long EntryCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public TimeSpan AverageGetTime { get; set; }
        public TimeSpan AverageSetTime { get; set; }
        public DateTime LastResetTime { get; set; }
        public Dictionary<string, long> OperationCounts { get; set; } = new();
        public Dictionary<string, TimeSpan> OperationTimings { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for cache eviction events.
    /// </summary>
    public class CacheEvictionEventArgs : EventArgs
    {
        public string Key { get; set; } = string.Empty;
        public CacheRegion Region { get; set; }
        public CacheEvictionReason Reason { get; set; }
        public DateTime EvictedAt { get; set; }
        public long? SizeInBytes { get; set; }
    }

    /// <summary>
    /// Reasons for cache eviction.
    /// </summary>
    public enum CacheEvictionReason
    {
        Expired,
        MemoryPressure,
        RegionCleared,
        Removed,
        Replaced,
        CapacityReached,
        PolicyTriggered
    }

    /// <summary>
    /// Event arguments for statistics update events.
    /// </summary>
    public class CacheStatisticsEventArgs : EventArgs
    {
        public CacheRegion Region { get; set; }
        public string Operation { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// Health status of the cache system.
    /// </summary>
    public class CacheHealthStatus
    {
        public bool IsHealthy { get; set; }
        public Dictionary<string, bool> ComponentStatus { get; set; } = new();
        public Dictionary<string, string> Issues { get; set; } = new();
        public DateTime CheckedAt { get; set; }
        public TimeSpan? MemoryCacheResponseTime { get; set; }
        public TimeSpan? DistributedCacheResponseTime { get; set; }
    }
}