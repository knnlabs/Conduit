using System;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Base class for all system notifications sent via SignalR.
    /// </summary>
    public abstract class SystemNotification
    {
        /// <summary>
        /// Gets or sets the notification ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp when the notification was created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the notification type.
        /// </summary>
        public abstract string Type { get; }

        /// <summary>
        /// Gets or sets the notification priority.
        /// </summary>
        public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;
    }

    /// <summary>
    /// Notification for provider health status changes.
    /// </summary>
    public class ProviderHealthNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "provider_health";

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the health status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets additional details about the health status.
        /// </summary>
        public string? Details { get; set; }
    }

    /// <summary>
    /// Notification for rate limit warnings.
    /// </summary>
    public class RateLimitNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "rate_limit";

        /// <summary>
        /// Gets or sets the number of requests remaining.
        /// </summary>
        public int Remaining { get; set; }

        /// <summary>
        /// Gets or sets when the rate limit resets.
        /// </summary>
        public DateTime ResetTime { get; set; }

        /// <summary>
        /// Gets or sets the affected endpoint.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the percentage of rate limit used.
        /// </summary>
        public double PercentageUsed { get; set; }
    }

    /// <summary>
    /// Notification for system announcements.
    /// </summary>
    public class SystemAnnouncementNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "system_announcement";

        /// <summary>
        /// Gets or sets the announcement message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the announcement.
        /// </summary>
        public string? Category { get; set; }
    }

    /// <summary>
    /// Notification for service degradation.
    /// </summary>
    public class ServiceDegradationNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "service_degradation";

        /// <summary>
        /// Gets or sets the degraded service name.
        /// </summary>
        public string Service { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for degradation.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the estimated time to resolution.
        /// </summary>
        public DateTime? EstimatedResolution { get; set; }
    }

    /// <summary>
    /// Notification for service restoration.
    /// </summary>
    public class ServiceRestorationNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "service_restoration";

        /// <summary>
        /// Gets or sets the restored service name.
        /// </summary>
        public string Service { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration of the outage.
        /// </summary>
        public TimeSpan? OutageDuration { get; set; }
    }

    /// <summary>
    /// Notification for model mapping changes.
    /// </summary>
    public class ModelMappingNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "model_mapping_changed";

        /// <summary>
        /// Gets or sets the mapping ID.
        /// </summary>
        public int MappingId { get; set; }

        /// <summary>
        /// Gets or sets the model alias.
        /// </summary>
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the change type (Created, Updated, Deleted).
        /// </summary>
        public string ChangeType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Notification for model capabilities discovery.
    /// </summary>
    public class ModelCapabilitiesNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "model_capabilities_discovered";

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of models.
        /// </summary>
        public int ModelCount { get; set; }

        /// <summary>
        /// Gets or sets the number of embedding models.
        /// </summary>
        public int EmbeddingCount { get; set; }

        /// <summary>
        /// Gets or sets the number of vision models.
        /// </summary>
        public int VisionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of image generation models.
        /// </summary>
        public int ImageGenCount { get; set; }

        /// <summary>
        /// Gets or sets the number of video generation models.
        /// </summary>
        public int VideoGenCount { get; set; }
    }

    /// <summary>
    /// Notification for model availability changes.
    /// </summary>
    public class ModelAvailabilityNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "model_availability_changed";

        /// <summary>
        /// Gets or sets the model identifier.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the model is available.
        /// </summary>
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Health status enumeration for provider health notifications.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// Service is healthy and responding normally.
        /// </summary>
        Healthy,

        /// <summary>
        /// Service is experiencing degraded performance.
        /// </summary>
        Degraded,

        /// <summary>
        /// Service is unhealthy or not responding.
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Priority levels for system notifications.
    /// </summary>
    public enum NotificationPriority
    {
        /// <summary>
        /// Low priority notification.
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority notification.
        /// </summary>
        Medium,

        /// <summary>
        /// High priority notification.
        /// </summary>
        High,

        /// <summary>
        /// Critical priority notification requiring immediate attention.
        /// </summary>
        Critical
    }
}