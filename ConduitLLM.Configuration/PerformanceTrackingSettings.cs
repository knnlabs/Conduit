namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Configuration settings for performance tracking in ConduitLLM.
    /// </summary>
    public class PerformanceTrackingSettings
    {
        /// <summary>
        /// Gets or sets whether performance tracking is enabled globally.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to include performance metrics in API responses.
        /// </summary>
        public bool IncludeInResponse { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to track streaming performance metrics.
        /// </summary>
        public bool TrackStreamingMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to store performance metrics for historical analysis.
        /// </summary>
        public bool StoreMetrics { get; set; } = false;

        /// <summary>
        /// Gets or sets the retention period in days for stored performance metrics.
        /// </summary>
        public int MetricsRetentionDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets specific providers to exclude from performance tracking.
        /// </summary>
        public List<string> ExcludedProviders { get; set; } = new();

        /// <summary>
        /// Gets or sets specific models to exclude from performance tracking.
        /// </summary>
        public List<string> ExcludedModels { get; set; } = new();
    }
}