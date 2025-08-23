namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Provider health status.
    /// </summary>
    public class ProviderHealthStatus
    {
        /// <summary>
        /// Provider type.
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Health status: healthy, degraded, or unhealthy.
        /// </summary>
        public string Status { get; set; } = "healthy";

        /// <summary>
        /// Last successful request timestamp.
        /// </summary>
        public DateTime? LastSuccessfulRequest { get; set; }

        /// <summary>
        /// Error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Average latency in milliseconds.
        /// </summary>
        public double AverageLatency { get; set; }

        /// <summary>
        /// Number of available models.
        /// </summary>
        public int AvailableModels { get; set; }

        /// <summary>
        /// Is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}