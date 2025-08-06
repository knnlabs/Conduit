namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Resource utilization metrics
    /// </summary>
    public class ResourceMetrics
    {
        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// Disk usage percentage
        /// </summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>
        /// Network I/O in MB/s
        /// </summary>
        public double NetworkIOMBps { get; set; }

        /// <summary>
        /// Thread count
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Connection pool stats
        /// </summary>
        public ConnectionPoolStats ConnectionPools { get; set; } = new();
    }
}