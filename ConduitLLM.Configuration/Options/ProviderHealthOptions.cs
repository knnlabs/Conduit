namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Options for configuring the provider health monitoring system.
    /// </summary>
    public class ProviderHealthOptions
    {
        /// <summary>
        /// The configuration section name for provider health options.
        /// </summary>
        public const string SectionName = "ProviderHealth";

        /// <summary>
        /// Indicates whether provider health monitoring is globally enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The default interval in minutes between health checks.
        /// </summary>
        public int DefaultCheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// The default timeout in seconds for health check requests.
        /// </summary>
        public int DefaultTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// The default number of retry attempts for failed health checks.
        /// </summary>
        public int DefaultRetryAttempts { get; set; } = 2;

        /// <summary>
        /// The number of days to retain detailed health records.
        /// </summary>
        public int DetailedRecordRetentionDays { get; set; } = 30;

        /// <summary>
        /// The number of months to retain summary health records.
        /// </summary>
        public int SummaryRecordRetentionMonths { get; set; } = 12;

        /// <summary>
        /// The base path for the health check endpoints.
        /// </summary>
        public string HealthCheckBasePath { get; set; } = "api/v1/health";
    }
}
