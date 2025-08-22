namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Model usage statistics.
    /// </summary>
    public class ModelUsageStats
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Provider type.
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Requests per minute.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Total tokens processed per minute.
        /// </summary>
        public long TokensPerMinute { get; set; }

        /// <summary>
        /// Average response time in milliseconds.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }
    }
}