using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object for cache health summary information
    /// </summary>
    public class CacheHealthSummaryDto
    {
        /// <summary>
        /// Overall health status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the health check
        /// </summary>
        public DateTime CheckTime { get; set; }

        /// <summary>
        /// Current cache metrics
        /// </summary>
        public CacheMonitoringStatusDto Metrics { get; set; } = new();

        /// <summary>
        /// Recent alerts
        /// </summary>
        public List<CacheAlertDto> RecentAlerts { get; set; } = new();

        /// <summary>
        /// Health check details
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();

        /// <summary>
        /// Overall health status
        /// </summary>
        public string OverallHealth { get; set; } = string.Empty;

        /// <summary>
        /// Cache hit rate percentage
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Eviction rate percentage
        /// </summary>
        public double EvictionRate { get; set; }

        /// <summary>
        /// Number of active alerts
        /// </summary>
        public int ActiveAlerts { get; set; }

        /// <summary>
        /// Total cache size
        /// </summary>
        public long TotalCacheSize { get; set; }

        /// <summary>
        /// Total number of cache entries
        /// </summary>
        public long TotalEntries { get; set; }

        /// <summary>
        /// Last health check timestamp
        /// </summary>
        public DateTime LastCheck { get; set; }
    }
}