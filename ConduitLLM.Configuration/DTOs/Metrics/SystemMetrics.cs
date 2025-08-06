using System;

namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// System resource metrics.
    /// </summary>
    public class SystemMetrics
    {
        /// <summary>
        /// CPU usage percentage.
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Memory usage in MB.
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Thread count.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Handle count.
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// GC memory in MB.
        /// </summary>
        public double GcMemoryMB { get; set; }

        /// <summary>
        /// Process uptime.
        /// </summary>
        public TimeSpan Uptime { get; set; }
    }
}