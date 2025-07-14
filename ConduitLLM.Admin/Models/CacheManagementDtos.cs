using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Admin.Models
{
    /// <summary>
    /// DTO for cache configuration response.
    /// </summary>
    public class CacheConfigurationDto
    {
        /// <summary>
        /// Response timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// List of cache policies.
        /// </summary>
        public List<CachePolicyDto> CachePolicies { get; set; } = new();

        /// <summary>
        /// List of cache regions.
        /// </summary>
        public List<CacheRegionDto> CacheRegions { get; set; } = new();

        /// <summary>
        /// Overall cache statistics.
        /// </summary>
        public CacheStatisticsDto Statistics { get; set; } = new();

        /// <summary>
        /// Global cache configuration.
        /// </summary>
        public CacheGlobalConfigDto Configuration { get; set; } = new();
    }

    /// <summary>
    /// DTO for cache policy information.
    /// </summary>
    public class CachePolicyDto
    {
        /// <summary>
        /// Policy identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Policy name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Policy type (memory, distributed, etc.).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Time-to-live in seconds.
        /// </summary>
        public int TTL { get; set; }

        /// <summary>
        /// Maximum size (items or bytes).
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Eviction strategy (LRU, LFU, etc.).
        /// </summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// Whether the policy is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Policy description.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for cache region information.
    /// </summary>
    public class CacheRegionDto
    {
        /// <summary>
        /// Region identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Region display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Cache type (memory, redis, etc.).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Current status (healthy, unhealthy, idle).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Number of nodes (for distributed cache).
        /// </summary>
        public int Nodes { get; set; }

        /// <summary>
        /// Region metrics.
        /// </summary>
        public CacheMetricsDto Metrics { get; set; } = new();
    }

    /// <summary>
    /// DTO for cache metrics.
    /// </summary>
    public class CacheMetricsDto
    {
        /// <summary>
        /// Current size (formatted string).
        /// </summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Number of items in cache.
        /// </summary>
        public long Items { get; set; }

        /// <summary>
        /// Cache hit rate percentage.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Cache miss rate percentage.
        /// </summary>
        public double MissRate { get; set; }

        /// <summary>
        /// Eviction rate percentage.
        /// </summary>
        public double EvictionRate { get; set; }
    }

    /// <summary>
    /// DTO for cache statistics.
    /// </summary>
    public class CacheStatisticsDto
    {
        /// <summary>
        /// Total cache hits.
        /// </summary>
        public long TotalHits { get; set; }

        /// <summary>
        /// Total cache misses.
        /// </summary>
        public long TotalMisses { get; set; }

        /// <summary>
        /// Overall hit rate percentage.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Average response times.
        /// </summary>
        public ResponseTimeDto AvgResponseTime { get; set; } = new();

        /// <summary>
        /// Memory usage information.
        /// </summary>
        public MemoryUsageDto MemoryUsage { get; set; } = new();

        /// <summary>
        /// Top cached items by hit count.
        /// </summary>
        public List<TopCachedItemDto> TopCachedItems { get; set; } = new();
    }

    /// <summary>
    /// DTO for response time metrics.
    /// </summary>
    public class ResponseTimeDto
    {
        /// <summary>
        /// Average response time with cache hit (ms).
        /// </summary>
        public int WithCache { get; set; }

        /// <summary>
        /// Average response time without cache (ms).
        /// </summary>
        public int WithoutCache { get; set; }
    }

    /// <summary>
    /// DTO for memory usage information.
    /// </summary>
    public class MemoryUsageDto
    {
        /// <summary>
        /// Current memory usage (formatted string).
        /// </summary>
        public string Current { get; set; } = string.Empty;

        /// <summary>
        /// Peak memory usage (formatted string).
        /// </summary>
        public string Peak { get; set; } = string.Empty;

        /// <summary>
        /// Memory limit (formatted string).
        /// </summary>
        public string Limit { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for top cached item information.
    /// </summary>
    public class TopCachedItemDto
    {
        /// <summary>
        /// Cache key or pattern.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Number of hits.
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Item size (formatted string).
        /// </summary>
        public string Size { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for global cache configuration.
    /// </summary>
    public class CacheGlobalConfigDto
    {
        /// <summary>
        /// Default TTL in seconds.
        /// </summary>
        public int DefaultTTL { get; set; }

        /// <summary>
        /// Maximum memory size.
        /// </summary>
        public string MaxMemorySize { get; set; } = string.Empty;

        /// <summary>
        /// Global eviction policy.
        /// </summary>
        public string EvictionPolicy { get; set; } = string.Empty;

        /// <summary>
        /// Whether compression is enabled.
        /// </summary>
        public bool CompressionEnabled { get; set; }

        /// <summary>
        /// Redis connection string (always redacted).
        /// </summary>
        public string? RedisConnectionString { get; set; }
    }

    /// <summary>
    /// DTO for updating cache configuration.
    /// </summary>
    public class UpdateCacheConfigDto
    {
        /// <summary>
        /// Default TTL in seconds.
        /// </summary>
        [Range(0, 86400, ErrorMessage = "TTL must be between 0 and 86400 seconds (24 hours)")]
        public int? DefaultTTLSeconds { get; set; }

        /// <summary>
        /// Maximum memory size.
        /// </summary>
        [RegularExpression(@"^\d+(\.\d+)?\s*(B|KB|MB|GB|TB)$", ErrorMessage = "Invalid memory size format")]
        public string? MaxMemorySize { get; set; }

        /// <summary>
        /// Eviction policy.
        /// </summary>
        [RegularExpression("^(LRU|LFU|FIFO|Random|Priority|TTL)$", ErrorMessage = "Invalid eviction policy")]
        public string? EvictionPolicy { get; set; }

        /// <summary>
        /// Enable or disable compression.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Clear affected caches after update.
        /// </summary>
        public bool ClearAffectedCaches { get; set; }

        /// <summary>
        /// Apply configuration globally to all regions.
        /// </summary>
        public bool ApplyGlobally { get; set; }

        /// <summary>
        /// Specific region ID to update (if not applying globally).
        /// </summary>
        public string? RegionId { get; set; }
    }

    /// <summary>
    /// DTO for cache entries listing.
    /// </summary>
    public class CacheEntriesDto
    {
        /// <summary>
        /// Region identifier.
        /// </summary>
        public string RegionId { get; set; } = string.Empty;

        /// <summary>
        /// List of cache entries.
        /// </summary>
        public List<CacheEntryDto> Entries { get; set; } = new();

        /// <summary>
        /// Total number of entries in the region.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of entries skipped.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Number of entries returned.
        /// </summary>
        public int Take { get; set; }

        /// <summary>
        /// Optional message (e.g., for restricted regions).
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// DTO for individual cache entry information.
    /// </summary>
    public class CacheEntryDto
    {
        /// <summary>
        /// Cache key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Entry size (formatted string).
        /// </summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last access timestamp.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Expiration timestamp.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Number of times accessed.
        /// </summary>
        public long AccessCount { get; set; }

        /// <summary>
        /// Entry priority.
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// DTO for updating cache policy.
    /// </summary>
    public class UpdateCachePolicyDto
    {
        /// <summary>
        /// New TTL in seconds.
        /// </summary>
        [Range(0, 86400, ErrorMessage = "TTL must be between 0 and 86400 seconds")]
        public int? TTL { get; set; }

        /// <summary>
        /// New maximum size.
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Max size must be greater than 0")]
        public int? MaxSize { get; set; }

        /// <summary>
        /// New eviction strategy.
        /// </summary>
        [RegularExpression("^(LRU|LFU|FIFO|Random|Priority|TTL)$", ErrorMessage = "Invalid eviction strategy")]
        public string? Strategy { get; set; }

        /// <summary>
        /// Enable or disable the policy.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Reason for the policy change.
        /// </summary>
        [Required]
        [StringLength(500, ErrorMessage = "Reason must not exceed 500 characters")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for cache health status.
    /// </summary>
    public class CacheHealthDto
    {
        /// <summary>
        /// Overall health status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Health check timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Individual region health statuses.
        /// </summary>
        public Dictionary<string, RegionHealthDto> Regions { get; set; } = new();

        /// <summary>
        /// Any health issues detected.
        /// </summary>
        public List<string> Issues { get; set; } = new();
    }

    /// <summary>
    /// DTO for individual region health.
    /// </summary>
    public class RegionHealthDto
    {
        /// <summary>
        /// Region status (healthy, degraded, unhealthy).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response time in milliseconds.
        /// </summary>
        public int ResponseTimeMs { get; set; }

        /// <summary>
        /// Whether the region is accessible.
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// Error message if unhealthy.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}