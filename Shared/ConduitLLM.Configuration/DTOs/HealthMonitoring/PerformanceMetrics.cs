namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Request rate per second
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Average response time
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// P95 response time
        /// </summary>
        public double P95ResponseTimeMs { get; set; }

        /// <summary>
        /// P99 response time
        /// </summary>
        public double P99ResponseTimeMs { get; set; }

        /// <summary>
        /// Error rate percentage
        /// </summary>
        public double ErrorRatePercent { get; set; }

        /// <summary>
        /// Active request count
        /// </summary>
        public int ActiveRequests { get; set; }

        /// <summary>
        /// Queue depth
        /// </summary>
        public int QueueDepth { get; set; }
    }
}