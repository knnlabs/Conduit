using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents a cache operation for statistics tracking.
    /// </summary>
    public class CacheOperation
    {
        /// <summary>
        /// The cache region.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Type of operation.
        /// </summary>
        public CacheOperationType OperationType { get; set; }

        /// <summary>
        /// Whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Duration of the operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Size of the data in bytes (if applicable).
        /// </summary>
        public long? DataSizeBytes { get; set; }

        /// <summary>
        /// Cache key involved in the operation.
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// When the operation occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional metadata about the operation.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Types of cache operations.
    /// </summary>
    public enum CacheOperationType
    {
        Get,
        Set,
        Remove,
        Clear,
        Refresh,
        Eviction,
        Hit,
        Miss
    }

    /// <summary>
    /// Comprehensive cache statistics for a region.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// The cache region these statistics apply to.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Total number of cache hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Total number of set operations.
        /// </summary>
        public long SetCount { get; set; }

        /// <summary>
        /// Total number of remove operations.
        /// </summary>
        public long RemoveCount { get; set; }

        /// <summary>
        /// Total number of evictions.
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// Total number of errors.
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Hit rate percentage.
        /// </summary>
        public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests * 100 : 0;

        /// <summary>
        /// Total number of get requests (hits + misses).
        /// </summary>
        public long TotalRequests => HitCount + MissCount;

        /// <summary>
        /// Current number of entries in cache.
        /// </summary>
        public long EntryCount { get; set; }

        /// <summary>
        /// Total memory used in bytes.
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Average response time for get operations.
        /// </summary>
        public TimeSpan AverageGetTime { get; set; }

        /// <summary>
        /// Average response time for set operations.
        /// </summary>
        public TimeSpan AverageSetTime { get; set; }

        /// <summary>
        /// 95th percentile response time for get operations.
        /// </summary>
        public TimeSpan P95GetTime { get; set; }

        /// <summary>
        /// 99th percentile response time for get operations.
        /// </summary>
        public TimeSpan P99GetTime { get; set; }

        /// <summary>
        /// Maximum response time recorded.
        /// </summary>
        public TimeSpan MaxResponseTime { get; set; }

        /// <summary>
        /// When statistics collection started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When statistics were last updated.
        /// </summary>
        public DateTime LastUpdateTime { get; set; }

        /// <summary>
        /// Duration of the statistics window.
        /// </summary>
        public TimeSpan Duration => LastUpdateTime - StartTime;

        /// <summary>
        /// Operations per second.
        /// </summary>
        public double OperationsPerSecond => Duration.TotalSeconds > 0 
            ? (HitCount + MissCount + SetCount + RemoveCount) / Duration.TotalSeconds 
            : 0;

        /// <summary>
        /// Breakdown by operation type.
        /// </summary>
        public Dictionary<CacheOperationType, long> OperationCounts { get; set; } = new();

        /// <summary>
        /// Response time percentiles.
        /// </summary>
        public Dictionary<int, TimeSpan> ResponseTimePercentiles { get; set; } = new();

        /// <summary>
        /// Size distribution of cached items.
        /// </summary>
        public SizeDistribution? SizeDistribution { get; set; }
    }

    /// <summary>
    /// Time-series statistics data point.
    /// </summary>
    public class TimeSeriesStatistics
    {
        /// <summary>
        /// Timestamp for this data point.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Statistics snapshot at this time.
        /// </summary>
        public CacheStatistics Statistics { get; set; } = null!;

        /// <summary>
        /// Interval this data point represents.
        /// </summary>
        public TimeSpan Interval { get; set; }
    }

    /// <summary>
    /// Size distribution statistics.
    /// </summary>
    public class SizeDistribution
    {
        /// <summary>
        /// Minimum size in bytes.
        /// </summary>
        public long MinSize { get; set; }

        /// <summary>
        /// Maximum size in bytes.
        /// </summary>
        public long MaxSize { get; set; }

        /// <summary>
        /// Average size in bytes.
        /// </summary>
        public long AverageSize { get; set; }

        /// <summary>
        /// Median size in bytes.
        /// </summary>
        public long MedianSize { get; set; }

        /// <summary>
        /// Size buckets for histogram.
        /// </summary>
        public Dictionary<string, long> Buckets { get; set; } = new();
    }

    /// <summary>
    /// Alert thresholds for cache monitoring.
    /// </summary>
    public class CacheAlertThresholds
    {
        /// <summary>
        /// Minimum acceptable hit rate percentage.
        /// </summary>
        public double? MinHitRate { get; set; }

        /// <summary>
        /// Maximum acceptable miss rate percentage.
        /// </summary>
        public double? MaxMissRate { get; set; }

        /// <summary>
        /// Maximum acceptable error rate percentage.
        /// </summary>
        public double? MaxErrorRate { get; set; }

        /// <summary>
        /// Maximum acceptable response time.
        /// </summary>
        public TimeSpan? MaxResponseTime { get; set; }

        /// <summary>
        /// Maximum acceptable P95 response time.
        /// </summary>
        public TimeSpan? MaxP95ResponseTime { get; set; }

        /// <summary>
        /// Maximum memory usage in bytes.
        /// </summary>
        public long? MaxMemoryUsageBytes { get; set; }

        /// <summary>
        /// Maximum memory usage percentage.
        /// </summary>
        public double? MaxMemoryUsagePercent { get; set; }

        /// <summary>
        /// Maximum eviction rate per minute.
        /// </summary>
        public double? MaxEvictionRate { get; set; }

        /// <summary>
        /// Whether alerts are enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Represents an active cache alert.
    /// </summary>
    public class CacheAlert
    {
        /// <summary>
        /// Unique identifier for the alert.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The cache region affected.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Type of alert.
        /// </summary>
        public CacheAlertType AlertType { get; set; }

        /// <summary>
        /// Severity of the alert.
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Current value that triggered the alert.
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Threshold value that was exceeded.
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// When the alert was triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional context about the alert.
        /// </summary>
        public Dictionary<string, object>? Context { get; set; }
    }

    /// <summary>
    /// Types of cache alerts.
    /// </summary>
    public enum CacheAlertType
    {
        LowHitRate,
        HighMissRate,
        HighErrorRate,
        SlowResponseTime,
        HighMemoryUsage,
        HighEvictionRate,
        CacheUnavailable
    }


    /// <summary>
    /// Event arguments for statistics updates.
    /// </summary>
    public class CacheStatisticsUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// The cache region that was updated.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// The updated statistics.
        /// </summary>
        public CacheStatistics Statistics { get; set; } = null!;

        /// <summary>
        /// The operation that triggered the update.
        /// </summary>
        public CacheOperation? TriggeringOperation { get; set; }
    }

    /// <summary>
    /// Event arguments for cache alerts.
    /// </summary>
    public class CacheAlertEventArgs : EventArgs
    {
        /// <summary>
        /// The triggered alert.
        /// </summary>
        public CacheAlert Alert { get; set; } = null!;

        /// <summary>
        /// Whether this is a new alert or an update.
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// Whether the alert condition has been resolved.
        /// </summary>
        public bool IsResolved { get; set; }
    }
}