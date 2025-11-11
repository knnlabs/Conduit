using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing alerts and notifications for image generation operations.
    /// </summary>
    public interface IImageGenerationAlertingService
    {
        /// <summary>
        /// Registers a new alert rule.
        /// </summary>
        /// <param name="rule">The alert rule to register.</param>
        /// <returns>The ID of the registered rule.</returns>
        Task<string> RegisterAlertRuleAsync(ImageGenerationAlertRule rule);

        /// <summary>
        /// Updates an existing alert rule.
        /// </summary>
        /// <param name="ruleId">The ID of the rule to update.</param>
        /// <param name="rule">The updated rule.</param>
        Task UpdateAlertRuleAsync(string ruleId, ImageGenerationAlertRule rule);

        /// <summary>
        /// Deletes an alert rule.
        /// </summary>
        /// <param name="ruleId">The ID of the rule to delete.</param>
        Task DeleteAlertRuleAsync(string ruleId);

        /// <summary>
        /// Gets all active alert rules.
        /// </summary>
        /// <returns>List of active alert rules.</returns>
        Task<List<ImageGenerationAlertRule>> GetActiveRulesAsync();

        /// <summary>
        /// Evaluates current metrics against alert rules.
        /// </summary>
        /// <param name="metrics">Current metrics snapshot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task EvaluateMetricsAsync(ImageGenerationMetricsSnapshot metrics, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets alert history within a time range.
        /// </summary>
        /// <param name="startTime">Start time.</param>
        /// <param name="endTime">End time.</param>
        /// <param name="severity">Optional severity filter.</param>
        /// <returns>List of triggered alerts.</returns>
        Task<List<ImageGenerationAlert>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, AlertSeverity? severity = null);

        /// <summary>
        /// Acknowledges an alert.
        /// </summary>
        /// <param name="alertId">The alert ID to acknowledge.</param>
        /// <param name="acknowledgedBy">Who acknowledged the alert.</param>
        /// <param name="notes">Optional acknowledgment notes.</param>
        Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null);

        /// <summary>
        /// Tests an alert rule to see if it would trigger.
        /// </summary>
        /// <param name="rule">The rule to test.</param>
        /// <returns>Test result with details.</returns>
        Task<AlertTestResult> TestAlertRuleAsync(ImageGenerationAlertRule rule);

        /// <summary>
        /// Gets active (unacknowledged) alerts.
        /// </summary>
        /// <returns>List of active alerts.</returns>
        Task<List<ImageGenerationAlert>> GetActiveAlertsAsync();

        /// <summary>
        /// Registers a notification channel.
        /// </summary>
        /// <param name="channel">The notification channel to register.</param>
        /// <returns>The ID of the registered channel.</returns>
        Task<string> RegisterNotificationChannelAsync(NotificationChannel channel);

        /// <summary>
        /// Tests a notification channel.
        /// </summary>
        /// <param name="channelId">The channel ID to test.</param>
        /// <returns>Test result.</returns>
        Task<NotificationTestResult> TestNotificationChannelAsync(string channelId);
    }

    /// <summary>
    /// Alert rule for image generation operations.
    /// </summary>
    public class ImageGenerationAlertRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ImageGenerationMetricType MetricType { get; set; }
        public AlertCondition Condition { get; set; } = new();
        public AlertSeverity Severity { get; set; }
        public bool IsEnabled { get; set; } = true;
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
        public List<string> NotificationChannelIds { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastTriggeredAt { get; set; }
    }

    /// <summary>
    /// Types of metrics that can trigger alerts.
    /// </summary>
    public enum ImageGenerationMetricType
    {
        ErrorRate,
        ResponseTime,
        P95ResponseTime,
        ProviderAvailability,
        ProviderHealthScore,
        QueueDepth,
        QueueWaitTime,
        GenerationRate,
        CostRate,
        CostPerImage,
        ResourceUtilization,
        ConsecutiveFailures,
        SlaViolation,
        VirtualKeyBudget,
        StorageQuota
    }


    /// <summary>
    /// Triggered alert instance.
    /// </summary>
    public class ImageGenerationAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ImageGenerationAlertRule Rule { get; set; } = new();
        public double MetricValue { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public AlertState State { get; set; } = AlertState.Active;
        public string? AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgmentNotes { get; set; }
        public List<NotificationResult> NotificationResults { get; set; } = new();
    }

}