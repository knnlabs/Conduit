namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Business metrics.
    /// </summary>
    public class BusinessMetrics
    {
        /// <summary>
        /// Active virtual keys count.
        /// </summary>
        public int ActiveVirtualKeys { get; set; }

        /// <summary>
        /// Total requests per minute.
        /// </summary>
        public int TotalRequestsPerMinute { get; set; }

        /// <summary>
        /// Cost metrics.
        /// </summary>
        public CostMetrics Costs { get; set; } = new();

        /// <summary>
        /// Model usage statistics.
        /// </summary>
        public List<ModelUsageStats> ModelUsage { get; set; } = new();

        /// <summary>
        /// Virtual key statistics.
        /// </summary>
        public List<VirtualKeyStats> TopVirtualKeys { get; set; } = new();
    }
}