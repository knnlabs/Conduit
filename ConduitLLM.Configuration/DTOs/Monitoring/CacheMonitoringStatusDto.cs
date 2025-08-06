using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object for cache monitoring status
    /// </summary>
    public class CacheMonitoringStatusDto
    {
        /// <summary>
        /// Timestamp of the last check (UTC)
        /// </summary>
        public DateTime LastCheck { get; set; }

        /// <summary>
        /// Indicates overall cache health status at the time of the check
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Current cache hit rate percentage
        /// </summary>
        public double CurrentHitRate { get; set; }

        /// <summary>
        /// Current memory usage percentage
        /// </summary>
        public double CurrentMemoryUsagePercent { get; set; }

        /// <summary>
        /// Current eviction rate percentage
        /// </summary>
        public double CurrentEvictionRate { get; set; }

        /// <summary>
        /// Current average response time in milliseconds
        /// </summary>
        public double CurrentResponseTimeMs { get; set; }

        /// <summary>
        /// Number of alerts currently active
        /// </summary>
        public int ActiveAlerts { get; set; }

        /// <summary>
        /// Additional structured data providing context for the alert
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
    }
}