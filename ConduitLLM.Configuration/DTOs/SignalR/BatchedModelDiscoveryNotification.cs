using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Represents a batched collection of model discovery notifications
    /// </summary>
    public class BatchedModelDiscoveryNotification
    {
        /// <summary>
        /// Unique batch identifier
        /// </summary>
        public string BatchId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// When this batch was created
        /// </summary>
        public DateTime BatchCreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time window this batch covers
        /// </summary>
        public BatchTimeWindow TimeWindow { get; set; } = new();

        /// <summary>
        /// Summary statistics for this batch
        /// </summary>
        public BatchSummary Summary { get; set; } = new();

        /// <summary>
        /// New models discovered (grouped by provider)
        /// </summary>
        public Dictionary<string, List<DiscoveredModelInfo>> NewModelsByProvider { get; set; } = new();

        /// <summary>
        /// Capability changes (grouped by severity)
        /// </summary>
        public Dictionary<NotificationSeverity, List<ModelCapabilitiesChangedNotification>> CapabilityChanges { get; set; } = new();

        /// <summary>
        /// Price updates (grouped by provider)
        /// </summary>
        public Dictionary<string, List<ModelPricingUpdatedNotification>> PriceUpdatesByProvider { get; set; } = new();

        /// <summary>
        /// Critical notifications that should be highlighted
        /// </summary>
        public List<CriticalNotification> CriticalNotifications { get; set; } = new();
    }

    /// <summary>
    /// Time window information for a batch
    /// </summary>
    public class BatchTimeWindow
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationSeconds => (int)(EndTime - StartTime).TotalSeconds;
    }

    /// <summary>
    /// Summary statistics for a batch
    /// </summary>
    public class BatchSummary
    {
        public int TotalNotifications { get; set; }
        public int NewModelsCount { get; set; }
        public int CapabilityChangesCount { get; set; }
        public int PriceUpdatesCount { get; set; }
        public int AffectedProvidersCount { get; set; }
        public List<string> AffectedProviders { get; set; } = new();
        public Dictionary<NotificationSeverity, int> NotificationsBySeverity { get; set; } = new();
    }

    /// <summary>
    /// Represents a critical notification that needs immediate attention
    /// </summary>
    public class CriticalNotification
    {
        public string Type { get; set; } = string.Empty;
        public ProviderType ProviderType { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Configuration for notification batching behavior
    /// </summary>
    public class NotificationBatchingOptions
    {
        /// <summary>
        /// Default time window for batching notifications (in seconds)
        /// </summary>
        public int DefaultBatchingWindowSeconds { get; set; } = 5;

        /// <summary>
        /// Maximum time to hold notifications before sending (in seconds)
        /// </summary>
        public int MaxBatchingDelaySeconds { get; set; } = 10;

        /// <summary>
        /// Maximum number of notifications in a single batch
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        /// <summary>
        /// Severity levels that bypass batching and are sent immediately
        /// </summary>
        public List<NotificationSeverity> ImmediateSeverityLevels { get; set; } = new() { NotificationSeverity.Critical };

        /// <summary>
        /// Whether batching is enabled globally
        /// </summary>
        public bool EnableBatching { get; set; } = true;
    }
}