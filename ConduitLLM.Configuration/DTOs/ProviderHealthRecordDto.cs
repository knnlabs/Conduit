using System;

using static ConduitLLM.Configuration.Entities.ProviderHealthRecord;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for provider health status record
    /// </summary>
    public class ProviderHealthRecordDto
    {
        /// <summary>
        /// The unique identifier for the health record
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the LLM provider that was checked
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// The status of the provider
        /// </summary>
        public StatusType Status { get; set; }

        /// <summary>
        /// Indicates whether the provider was online at the time of checking
        /// </summary>
        public bool IsOnline => Status == StatusType.Online;

        /// <summary>
        /// A human-readable status message describing the health check result
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp when the health check was performed
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// The response time in milliseconds for the health check request
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Categorization of the error if the check failed (e.g., Network, Authentication, RateLimit)
        /// </summary>
        public string? ErrorCategory { get; set; }

        /// <summary>
        /// Detailed error information if the check failed
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// The endpoint URL that was checked
        /// </summary>
        public string? EndpointUrl { get; set; }
    }

    /// <summary>
    /// Data transfer object for provider health summary
    /// </summary>
    public class ProviderHealthSummaryDto
    {
        /// <summary>
        /// The name of the provider
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// The current status of the provider
        /// </summary>
        public StatusType Status { get; set; }

        /// <summary>
        /// A human-readable status message
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Uptime percentage (0-100) for the specified time period
        /// </summary>
        public double UptimePercentage { get; set; }

        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Total number of errors during the specified time period
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Distribution of error categories
        /// </summary>
        public Dictionary<string, int> ErrorCategories { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The last time this provider was checked
        /// </summary>
        public DateTime? LastCheckedUtc { get; set; }

        /// <summary>
        /// Indicates whether monitoring is enabled for this provider
        /// </summary>
        public bool MonitoringEnabled { get; set; }
    }

    /// <summary>
    /// Data transfer object for provider health statistics
    /// </summary>
    public class ProviderHealthStatisticsDto
    {
        /// <summary>
        /// Total number of providers being monitored
        /// </summary>
        public int TotalProviders { get; set; }

        /// <summary>
        /// Number of providers currently online
        /// </summary>
        public int OnlineProviders { get; set; }

        /// <summary>
        /// Number of providers currently offline
        /// </summary>
        public int OfflineProviders { get; set; }

        /// <summary>
        /// Number of providers with unknown status
        /// </summary>
        public int UnknownProviders { get; set; }

        /// <summary>
        /// Average response time across all providers, in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Total number of errors across all providers during the specified time period
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Distribution of error categories across all providers
        /// </summary>
        public Dictionary<string, int> ErrorCategoryDistribution { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The time period in hours for which statistics were calculated
        /// </summary>
        public int TimePeriodHours { get; set; }
    }
}
