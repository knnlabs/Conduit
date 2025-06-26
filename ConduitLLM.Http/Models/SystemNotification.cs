using System;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Base class for all system notifications.
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

}