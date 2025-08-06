namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object for updating cache monitoring thresholds
    /// </summary>
    public class UpdateThresholdsRequestDto
    {
        /// <summary>
        /// Updated minimum hit rate; null to keep current value
        /// </summary>
        public double? MinHitRate { get; set; }

        /// <summary>
        /// Updated maximum memory usage; null to keep current value
        /// </summary>
        public double? MaxMemoryUsage { get; set; }

        /// <summary>
        /// Updated maximum eviction rate; null to keep current value
        /// </summary>
        public double? MaxEvictionRate { get; set; }

        /// <summary>
        /// Updated maximum response time in ms; null to keep current value
        /// </summary>
        public double? MaxResponseTimeMs { get; set; }

        /// <summary>
        /// Updated minimum requests threshold; null to keep current value
        /// </summary>
        public long? MinRequestsForHitRateAlert { get; set; }
    }
}