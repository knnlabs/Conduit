using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for batching cache invalidation requests to optimize Redis operations
    /// </summary>
    public interface IBatchCacheInvalidationService
    {
        /// <summary>
        /// Queue a single invalidation request
        /// </summary>
        Task QueueInvalidationAsync<T>(string key, T eventData, CacheType cacheType) where T : DomainEvent;
        
        /// <summary>
        /// Queue multiple invalidation requests
        /// </summary>
        Task QueueBulkInvalidationAsync<T>(string[] keys, T eventData, CacheType cacheType) where T : DomainEvent;
        
        /// <summary>
        /// Configure batch invalidation options
        /// </summary>
        void Configure(BatchInvalidationOptions options);
        
        /// <summary>
        /// Get current batch statistics
        /// </summary>
        Task<BatchInvalidationStats> GetStatsAsync();
        
        /// <summary>
        /// Force immediate processing of all queued invalidations
        /// </summary>
        Task FlushAsync();
        
        /// <summary>
        /// Get current queue statistics
        /// </summary>
        Task<QueueStats> GetQueueStatsAsync();
        
        /// <summary>
        /// Get error rate for a specific time window
        /// </summary>
        Task<double> GetErrorRateAsync(TimeSpan window);
    }
    
    /// <summary>
    /// Types of caches that can be invalidated
    /// </summary>
    public enum CacheType
    {
        VirtualKey,
        ModelCost,
        Provider,
        ModelMapping,
        GlobalSetting,
        IpFilter
    }
    
    /// <summary>
    /// Configuration options for batch invalidation
    /// </summary>
    public class BatchInvalidationOptions
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan BatchWindow { get; set; } = TimeSpan.FromMilliseconds(100);
        public int MaxBatchSize { get; set; } = 100;
        public bool EnableCoalescing { get; set; } = true;
        public Dictionary<CacheType, int> PriorityWeights { get; set; } = new();
    }
    
    /// <summary>
    /// Statistics for batch invalidation operations
    /// </summary>
    public class BatchInvalidationStats
    {
        public long TotalQueued { get; set; }
        public long TotalProcessed { get; set; }
        public long TotalCoalesced { get; set; }
        public Dictionary<CacheType, CacheTypeStats> ByType { get; set; } = new();
        public TimeSpan AverageBatchProcessTime { get; set; }
        public double CoalescingRate => TotalQueued > 0 ? TotalCoalesced / (double)TotalQueued : 0;
    }
    
    /// <summary>
    /// Statistics for a specific cache type
    /// </summary>
    public class CacheTypeStats
    {
        public long Queued { get; set; }
        public long Processed { get; set; }
        public long Coalesced { get; set; }
        public long Errors { get; set; }
        public TimeSpan AverageProcessTime { get; set; }
    }
    
    /// <summary>
    /// Current queue depth statistics
    /// </summary>
    public class QueueStats
    {
        public int TotalQueueDepth { get; set; }
        public Dictionary<CacheType, int> QueueDepthByType { get; set; } = new();
        public DateTime LastProcessed { get; set; }
    }
    
    /// <summary>
    /// Represents a single invalidation request
    /// </summary>
    public class InvalidationRequest
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public InvalidationPriority Priority { get; set; } = InvalidationPriority.Normal;
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
        public DomainEvent? SourceEvent { get; set; }
    }
    
    /// <summary>
    /// Priority levels for cache invalidation
    /// </summary>
    public enum InvalidationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
    
    /// <summary>
    /// Result of a batch invalidation operation
    /// </summary>
    public class BatchInvalidationResult
    {
        public bool Success { get; set; }
        public int ProcessedCount { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Error { get; set; }
    }
}