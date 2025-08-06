namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object for cache monitoring alert thresholds
    /// </summary>
    public class CacheAlertThresholdsDto
    {
        /// <summary>
        /// Minimum acceptable cache hit rate percentage before an alert is raised
        /// </summary>
        public double MinHitRate { get; set; }

        /// <summary>
        /// Maximum allowed memory usage percentage before an alert is raised
        /// </summary>
        public double MaxMemoryUsage { get; set; }

        /// <summary>
        /// Maximum allowed percentage of evictions over the sampling window
        /// </summary>
        public double MaxEvictionRate { get; set; }

        /// <summary>
        /// Maximum average cache response time (in milliseconds) before an alert is raised
        /// </summary>
        public double MaxResponseTimeMs { get; set; }

        /// <summary>
        /// Minimum number of requests required before evaluating hit-rate alert logic
        /// </summary>
        public long MinRequestsForHitRateAlert { get; set; }
    }
}