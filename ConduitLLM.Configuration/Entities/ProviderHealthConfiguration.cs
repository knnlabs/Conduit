using System;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents configuration settings for monitoring the health of an LLM provider.
    /// </summary>
    public class ProviderHealthConfiguration
    {
        /// <summary>
        /// The unique identifier for the configuration.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the LLM provider that this configuration applies to.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether health monitoring is enabled for this provider.
        /// </summary>
        public bool MonitoringEnabled { get; set; } = true;

        /// <summary>
        /// The interval in minutes between health checks for this provider.
        /// </summary>
        public int CheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// The timeout in seconds for health check requests to this provider.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// The number of consecutive failures required before marking the provider as down.
        /// </summary>
        public int ConsecutiveFailuresThreshold { get; set; } = 3;

        /// <summary>
        /// Indicates whether notifications should be sent when this provider's status changes.
        /// </summary>
        public bool NotificationsEnabled { get; set; } = true;

        /// <summary>
        /// An optional custom endpoint URL to check for this provider.
        /// If null, the default endpoint for the provider will be used.
        /// </summary>
        public string? CustomEndpointUrl { get; set; }

        /// <summary>
        /// The last time this provider was checked, in UTC.
        /// </summary>
        public DateTime? LastCheckedUtc { get; set; }
    }
}