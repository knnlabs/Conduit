using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Represents subscription filters for model discovery notifications
    /// </summary>
    public class ModelDiscoverySubscriptionFilter
    {
        /// <summary>
        /// List of provider types to receive notifications for (null/empty = all providers)
        /// </summary>
        public List<ProviderType>? ProviderTypes { get; set; }

        /// <summary>
        /// List of capabilities to filter by (null/empty = all capabilities)
        /// </summary>
        public List<string>? Capabilities { get; set; }

        /// <summary>
        /// Whether to receive notifications for price changes
        /// </summary>
        public bool NotifyOnPriceChanges { get; set; } = true;

        /// <summary>
        /// Minimum percentage change in price to trigger a notification (0 = all changes)
        /// </summary>
        public decimal MinPriceChangePercentage { get; set; } = 0;

        /// <summary>
        /// Minimum severity level for notifications
        /// </summary>
        public NotificationSeverity MinSeverityLevel { get; set; } = NotificationSeverity.Low;

        /// <summary>
        /// Whether to batch notifications within a time window
        /// </summary>
        public bool EnableBatching { get; set; } = true;

        /// <summary>
        /// Custom batching window in seconds (null = use default)
        /// </summary>
        public int? BatchingWindowSeconds { get; set; }

        /// <summary>
        /// Maximum number of notifications to include in a single batch
        /// </summary>
        public int MaxBatchSize { get; set; } = 50;
    }

    /// <summary>
    /// Notification severity levels
    /// </summary>
    public enum NotificationSeverity
    {
        /// <summary>
        /// Minor metadata updates, small price changes
        /// </summary>
        Low = 0,

        /// <summary>
        /// Price updates > 10%, capability additions
        /// </summary>
        Medium = 1,

        /// <summary>
        /// New models, major capability changes
        /// </summary>
        High = 2,

        /// <summary>
        /// New provider, provider offline, critical changes
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Stores subscription information for a connection
    /// </summary>
    public class ModelDiscoverySubscription
    {
        public string ConnectionId { get; set; } = string.Empty;
        public Guid VirtualKeyId { get; set; }
        public ModelDiscoverySubscriptionFilter Filter { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}