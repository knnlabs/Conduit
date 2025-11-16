namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Database metrics.
    /// </summary>
    public class DatabaseMetrics
    {
        /// <summary>
        /// Number of active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Number of available connections.
        /// </summary>
        public int AvailableConnections { get; set; }

        /// <summary>
        /// Connection pool utilization percentage.
        /// </summary>
        public double PoolUtilization { get; set; }

        /// <summary>
        /// Average query duration in milliseconds.
        /// </summary>
        public double AverageQueryDuration { get; set; }

        /// <summary>
        /// Database errors per minute.
        /// </summary>
        public int ErrorsPerMinute { get; set; }

        /// <summary>
        /// Health status.
        /// </summary>
        public string HealthStatus { get; set; } = "healthy";
    }
}