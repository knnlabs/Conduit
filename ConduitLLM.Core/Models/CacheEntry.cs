using System;
using System.Collections.Generic;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a cache entry with metadata for management and monitoring.
    /// </summary>
    public class CacheEntry<T> : ICacheEntry
    {
        /// <summary>
        /// The cached value.
        /// </summary>
        public T Value { get; set; } = default!;

        /// <summary>
        /// Gets the value as an object for the interface.
        /// </summary>
        object? ICacheEntry.Value => Value;

        /// <summary>
        /// The cache key used to store this entry.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The cache region this entry belongs to.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// When the entry was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the entry was last accessed.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// When the entry expires.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Number of times this entry has been accessed.
        /// </summary>
        public long AccessCount { get; set; }

        /// <summary>
        /// Size of the entry in bytes (if calculable).
        /// </summary>
        public long? SizeInBytes { get; set; }

        /// <summary>
        /// Optional metadata associated with the entry.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Priority for eviction (higher values are kept longer).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Whether this entry should be compressed.
        /// </summary>
        public bool CompressData { get; set; }

        /// <summary>
        /// Source of the data (e.g., "database", "api", "computed").
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// Checks if the entry is expired.
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

        /// <summary>
        /// Gets the age of the entry.
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - CreatedAt;

        /// <summary>
        /// Gets the time since last access.
        /// </summary>
        public TimeSpan TimeSinceLastAccess => DateTime.UtcNow - LastAccessedAt;

        /// <summary>
        /// Updates the last accessed time and increments access count.
        /// </summary>
        public void RecordAccess()
        {
            LastAccessedAt = DateTime.UtcNow;
            AccessCount++;
        }
    }

    /// <summary>
    /// Configuration for cache entries in a specific region.
    /// </summary>
    public class CacheRegionConfig
    {
        /// <summary>
        /// The cache region this configuration applies to.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Whether caching is enabled for this region.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default time-to-live for entries in this region.
        /// </summary>
        public TimeSpan? DefaultTTL { get; set; }

        /// <summary>
        /// Maximum time-to-live for entries (prevents overly long caching).
        /// </summary>
        public TimeSpan? MaxTTL { get; set; }

        /// <summary>
        /// Maximum number of entries allowed in this region.
        /// </summary>
        public int? MaxEntries { get; set; }

        /// <summary>
        /// Maximum total size in bytes for this region.
        /// </summary>
        public long? MaxSizeInBytes { get; set; }

        /// <summary>
        /// Eviction policy when limits are reached.
        /// </summary>
        public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LRU;

        /// <summary>
        /// Whether to compress data in this region.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Minimum size in bytes before compression is applied.
        /// </summary>
        public long CompressionThreshold { get; set; } = 1024; // 1KB

        /// <summary>
        /// Whether to use distributed cache for this region.
        /// </summary>
        public bool UseDistributedCache { get; set; } = true;

        /// <summary>
        /// Whether to use in-memory cache for this region.
        /// </summary>
        public bool UseMemoryCache { get; set; } = true;

        /// <summary>
        /// Priority for this region during memory pressure (higher = more important).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Whether to track detailed statistics for this region.
        /// </summary>
        public bool EnableDetailedStats { get; set; } = true;

        /// <summary>
        /// Custom configuration values for this region.
        /// </summary>
        public Dictionary<string, object>? CustomConfig { get; set; }
    }

    /// <summary>
    /// Defines cache eviction policies.
    /// </summary>
    public enum CacheEvictionPolicy
    {
        /// <summary>
        /// Least Recently Used - evicts entries that haven't been accessed recently.
        /// </summary>
        LRU,

        /// <summary>
        /// Least Frequently Used - evicts entries with the lowest access count.
        /// </summary>
        LFU,

        /// <summary>
        /// First In First Out - evicts oldest entries first.
        /// </summary>
        FIFO,

        /// <summary>
        /// Random eviction - evicts random entries.
        /// </summary>
        Random,

        /// <summary>
        /// Priority-based eviction - evicts lowest priority entries first.
        /// </summary>
        Priority,

        /// <summary>
        /// Time-based eviction - evicts entries closest to expiration.
        /// </summary>
        TTL,

        /// <summary>
        /// Size-based eviction - evicts largest entries first.
        /// </summary>
        Size,

        /// <summary>
        /// Cost-based eviction - evicts entries with lowest cost/benefit ratio.
        /// </summary>
        Cost
    }
}