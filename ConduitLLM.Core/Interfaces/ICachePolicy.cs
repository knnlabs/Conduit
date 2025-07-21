using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Base interface for all cache policies.
    /// </summary>
    public interface ICachePolicy
    {
        /// <summary>
        /// Gets the policy name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the policy type.
        /// </summary>
        CachePolicyType PolicyType { get; }

        /// <summary>
        /// Gets whether this policy is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets the priority of this policy when multiple policies apply.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        bool Validate();
    }

    /// <summary>
    /// Interface for TTL (Time-To-Live) policies.
    /// </summary>
    public interface ITtlPolicy : ICachePolicy
    {
        /// <summary>
        /// Calculates the expiration time for a cache entry.
        /// </summary>
        /// <param name="entry">The cache entry being stored.</param>
        /// <param name="context">Context information for the cache operation.</param>
        /// <returns>The expiration time, or null for no expiration.</returns>
        DateTime? CalculateExpiration(ICacheEntry entry, CachePolicyContext context);

        /// <summary>
        /// Determines if an entry should be refreshed.
        /// </summary>
        /// <param name="entry">The cache entry to check.</param>
        /// <param name="context">Context information for the cache operation.</param>
        /// <returns>True if the entry should be refreshed.</returns>
        bool ShouldRefresh(ICacheEntry entry, CachePolicyContext context);
    }

    /// <summary>
    /// Interface for size-based cache policies.
    /// </summary>
    public interface ISizePolicy : ICachePolicy
    {
        /// <summary>
        /// Gets the maximum allowed size for the cache region.
        /// </summary>
        long? MaxSize { get; }

        /// <summary>
        /// Gets the size unit (items or bytes).
        /// </summary>
        CacheSizeUnit SizeUnit { get; }

        /// <summary>
        /// Calculates the size of a cache entry.
        /// </summary>
        /// <param name="entry">The cache entry.</param>
        /// <returns>The calculated size.</returns>
        long CalculateSize(ICacheEntry entry);

        /// <summary>
        /// Determines if adding an entry would exceed size limits.
        /// </summary>
        /// <param name="entry">The entry to add.</param>
        /// <param name="currentSize">Current cache size.</param>
        /// <returns>True if size limit would be exceeded.</returns>
        bool WouldExceedLimit(ICacheEntry entry, long currentSize);
    }

    /// <summary>
    /// Interface for cache eviction policies.
    /// </summary>
    public interface IEvictionPolicy : ICachePolicy
    {
        /// <summary>
        /// Selects entries for eviction when space is needed.
        /// </summary>
        /// <param name="entries">All cache entries.</param>
        /// <param name="spaceNeeded">Amount of space needed.</param>
        /// <param name="context">Context information.</param>
        /// <returns>Entries to evict.</returns>
        Task<IEnumerable<ICacheEntry>> SelectForEvictionAsync(
            IEnumerable<ICacheEntry> entries, 
            long spaceNeeded, 
            CachePolicyContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates entry metadata after access.
        /// </summary>
        /// <param name="entry">The accessed entry.</param>
        void OnEntryAccessed(ICacheEntry entry);

        /// <summary>
        /// Calculates the eviction score for an entry.
        /// </summary>
        /// <param name="entry">The cache entry.</param>
        /// <returns>Eviction score (lower scores evicted first).</returns>
        double CalculateEvictionScore(ICacheEntry entry);
    }

    /// <summary>
    /// Context information for policy decisions.
    /// </summary>
    public class CachePolicyContext
    {
        /// <summary>
        /// Gets or sets the cache region.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public CacheOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the current cache statistics.
        /// </summary>
        public CacheRegionStatistics? Statistics { get; set; }

        /// <summary>
        /// Gets or sets custom metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the requester identity.
        /// </summary>
        public string? RequesterId { get; set; }

        /// <summary>
        /// Gets or sets the request timestamp.
        /// </summary>
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of cache policies.
    /// </summary>
    public enum CachePolicyType
    {
        /// <summary>
        /// Time-to-live policy.
        /// </summary>
        TTL,

        /// <summary>
        /// Size-based policy.
        /// </summary>
        Size,

        /// <summary>
        /// Eviction policy.
        /// </summary>
        Eviction,

        /// <summary>
        /// Compression policy.
        /// </summary>
        Compression,

        /// <summary>
        /// Custom policy type.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Units for size policies.
    /// </summary>
    public enum CacheSizeUnit
    {
        /// <summary>
        /// Number of items.
        /// </summary>
        Items,

        /// <summary>
        /// Size in bytes.
        /// </summary>
        Bytes,

        /// <summary>
        /// Size in kilobytes.
        /// </summary>
        Kilobytes,

        /// <summary>
        /// Size in megabytes.
        /// </summary>
        Megabytes
    }

    /// <summary>
    /// Interface for cache entries with policy metadata.
    /// </summary>
    public interface ICacheEntry
    {
        /// <summary>
        /// Gets the cache key.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the cached value.
        /// </summary>
        object? Value { get; }

        /// <summary>
        /// Gets the cache region.
        /// </summary>
        CacheRegion Region { get; }

        /// <summary>
        /// Gets when the entry was created.
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Gets when the entry was last accessed.
        /// </summary>
        DateTime LastAccessedAt { get; }

        /// <summary>
        /// Gets the access count.
        /// </summary>
        long AccessCount { get; }

        /// <summary>
        /// Gets the entry size in bytes.
        /// </summary>
        long? SizeInBytes { get; }

        /// <summary>
        /// Gets the entry priority.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets custom metadata.
        /// </summary>
        Dictionary<string, object>? Metadata { get; }

        /// <summary>
        /// Gets the expiration time.
        /// </summary>
        DateTime? ExpiresAt { get; }

        /// <summary>
        /// Updates the last accessed time and increments access count.
        /// </summary>
        void RecordAccess();
    }
}