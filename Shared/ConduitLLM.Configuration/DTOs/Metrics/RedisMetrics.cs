namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Redis cache metrics.
    /// </summary>
    public class RedisMetrics
    {
        /// <summary>
        /// Memory usage in MB.
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Total number of keys.
        /// </summary>
        public long KeyCount { get; set; }

        /// <summary>
        /// Connected clients.
        /// </summary>
        public int ConnectedClients { get; set; }

        /// <summary>
        /// Cache hit rate percentage.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Operations per second.
        /// </summary>
        public double OperationsPerSecond { get; set; }

        /// <summary>
        /// Average operation latency in milliseconds.
        /// </summary>
        public double AverageLatency { get; set; }

        /// <summary>
        /// Connection status.
        /// </summary>
        public bool IsConnected { get; set; }
    }
}