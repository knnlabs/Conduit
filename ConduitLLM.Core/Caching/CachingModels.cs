using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Caching
{
    /// <summary>
    /// Defines the behavior for caching model responses
    /// </summary>
    public enum CacheBehavior
    {
        /// <summary>
        /// Use the default caching behavior configured globally
        /// </summary>
        Default,
        
        /// <summary>
        /// Always cache responses for this model regardless of global settings
        /// </summary>
        Always,
        
        /// <summary>
        /// Never cache responses for this model regardless of global settings
        /// </summary>
        Never
    }

    /// <summary>
    /// Configuration rule for model-specific caching behavior
    /// </summary>
    public class ModelCacheRule
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Pattern to match model names. Can include wildcard characters (*)
        /// </summary>
        public string ModelNamePattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Caching behavior for models matching the pattern
        /// </summary>
        public CacheBehavior CacheBehavior { get; set; } = CacheBehavior.Default;
        
        /// <summary>
        /// Optional custom expiration time in minutes. If null, uses the default expiration time.
        /// </summary>
        public int? ExpirationMinutes { get; set; }
    }

    /// <summary>
    /// Statistics about the cache's performance and utilization
    /// </summary>
    public class CacheStats
    {
        /// <summary>
        /// Total number of items currently in the cache
        /// </summary>
        public int TotalItems { get; set; }
        
        /// <summary>
        /// Cache hit rate (0.0 to 1.0)
        /// </summary>
        public double HitRate { get; set; }
        
        /// <summary>
        /// Estimated memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AvgResponseTime { get; set; }
    }
}
