using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for audio alerting and monitoring.
    /// </summary>
    public interface IAudioAlertingService
    {
        /// <summary>
        /// Registers an alert rule.
        /// </summary>
        /// <param name="rule">The alert rule to register.</param>
        /// <returns>The rule ID.</returns>
        Task<string> RegisterAlertRuleAsync(AudioAlertRule rule);

        /// <summary>
        /// Updates an existing alert rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <param name="rule">The updated rule.</param>
        Task UpdateAlertRuleAsync(string ruleId, AudioAlertRule rule);

        /// <summary>
        /// Deletes an alert rule.
        /// </summary>
        /// <param name="ruleId">The rule ID to delete.</param>
        Task DeleteAlertRuleAsync(string ruleId);

        /// <summary>
        /// Gets all active alert rules.
        /// </summary>
        /// <returns>List of active alert rules.</returns>
        Task<List<AudioAlertRule>> GetActiveRulesAsync();

        /// <summary>
        /// Evaluates metrics against alert rules.
        /// </summary>
        /// <param name="metrics">The metrics to evaluate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task EvaluateMetricsAsync(
            AudioMetricsSnapshot metrics,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets alert history.
        /// </summary>
        /// <param name="startTime">Start time for history.</param>
        /// <param name="endTime">End time for history.</param>
        /// <param name="severity">Optional severity filter.</param>
        /// <returns>List of triggered alerts.</returns>
        Task<List<TriggeredAlert>> GetAlertHistoryAsync(
            DateTime startTime,
            DateTime endTime,
            AlertSeverity? severity = null);

        /// <summary>
        /// Acknowledges an alert.
        /// </summary>
        /// <param name="alertId">The alert ID to acknowledge.</param>
        /// <param name="acknowledgedBy">Who acknowledged the alert.</param>
        /// <param name="notes">Optional notes.</param>
        Task AcknowledgeAlertAsync(
            string alertId,
            string acknowledgedBy,
            string? notes = null);

        /// <summary>
        /// Tests an alert rule.
        /// </summary>
        /// <param name="rule">The rule to test.</param>
        /// <returns>Test results.</returns>
        Task<AlertTestResult> TestAlertRuleAsync(AudioAlertRule rule);
    }

    /// <summary>
    /// Audio alert rule definition.
    /// </summary>
    public class AudioAlertRule
    {
        /// <summary>
        /// Gets or sets the rule ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the rule name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rule description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets whether the rule is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the metric to monitor.
        /// </summary>
        public AudioMetricType MetricType { get; set; }

        /// <summary>
        /// Gets or sets the condition.
        /// </summary>
        public AlertCondition Condition { get; set; } = new();

        /// <summary>
        /// Gets or sets the severity.
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the notification channels.
        /// </summary>
        public List<NotificationChannel> NotificationChannels { get; set; } = new();

        /// <summary>
        /// Gets or sets the cooldown period.
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets custom tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Types of audio metrics to monitor.
    /// </summary>
    public enum AudioMetricType
    {
        /// <summary>
        /// Error rate across all operations.
        /// </summary>
        ErrorRate,

        /// <summary>
        /// Average latency.
        /// </summary>
        Latency,

        /// <summary>
        /// Provider availability.
        /// </summary>
        ProviderAvailability,

        /// <summary>
        /// Cache hit rate.
        /// </summary>
        CacheHitRate,

        /// <summary>
        /// Active sessions count.
        /// </summary>
        ActiveSessions,

        /// <summary>
        /// Request rate.
        /// </summary>
        RequestRate,

        /// <summary>
        /// Cost per hour.
        /// </summary>
        CostRate,

        /// <summary>
        /// Connection pool utilization.
        /// </summary>
        ConnectionPoolUtilization,

        /// <summary>
        /// Audio processing queue length.
        /// </summary>
        QueueLength,

        /// <summary>
        /// Custom metric.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Alert condition.
    /// </summary>
    public class AlertCondition
    {
        /// <summary>
        /// Gets or sets the operator.
        /// </summary>
        public ComparisonOperator Operator { get; set; }

        /// <summary>
        /// Gets or sets the threshold value.
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Gets or sets the time window for evaluation.
        /// </summary>
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the minimum occurrences.
        /// </summary>
        public int MinimumOccurrences { get; set; } = 1;

        /// <summary>
        /// Gets or sets provider filter.
        /// </summary>
        public string? ProviderFilter { get; set; }

        /// <summary>
        /// Gets or sets custom expression.
        /// </summary>
        public string? CustomExpression { get; set; }
    }

    /// <summary>
    /// Comparison operators.
    /// </summary>
    public enum ComparisonOperator
    {
        /// <summary>
        /// Greater than.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// Less than.
        /// </summary>
        LessThan,

        /// <summary>
        /// Equal to.
        /// </summary>
        Equals,

        /// <summary>
        /// Not equal to.
        /// </summary>
        NotEquals,

        /// <summary>
        /// Greater than or equal.
        /// </summary>
        GreaterThanOrEqual,

        /// <summary>
        /// Less than or equal.
        /// </summary>
        LessThanOrEqual
    }

    /// <summary>
    /// Alert severity levels.
    /// </summary>
    public enum AlertSeverity
    {
        /// <summary>
        /// Informational alert.
        /// </summary>
        Info,

        /// <summary>
        /// Warning alert.
        /// </summary>
        Warning,

        /// <summary>
        /// Error alert.
        /// </summary>
        Error,

        /// <summary>
        /// Critical alert.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Notification channel configuration.
    /// </summary>
    public class NotificationChannel
    {
        /// <summary>
        /// Gets or sets the channel type.
        /// </summary>
        public NotificationChannelType Type { get; set; }

        /// <summary>
        /// Gets or sets the target (email, webhook URL, etc.).
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets channel-specific configuration.
        /// </summary>
        public Dictionary<string, string> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Types of notification channels.
    /// </summary>
    public enum NotificationChannelType
    {
        /// <summary>
        /// Email notification.
        /// </summary>
        Email,

        /// <summary>
        /// Webhook notification.
        /// </summary>
        Webhook,

        /// <summary>
        /// Slack notification.
        /// </summary>
        Slack,

        /// <summary>
        /// Teams notification.
        /// </summary>
        Teams,

        /// <summary>
        /// PagerDuty integration.
        /// </summary>
        PagerDuty,

        /// <summary>
        /// SMS notification.
        /// </summary>
        Sms,

        /// <summary>
        /// Custom notification.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Triggered alert instance.
    /// </summary>
    public class TriggeredAlert
    {
        /// <summary>
        /// Gets or sets the alert ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the rule that triggered this alert.
        /// </summary>
        public AudioAlertRule Rule { get; set; } = new();

        /// <summary>
        /// Gets or sets when the alert was triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the metric value that triggered the alert.
        /// </summary>
        public double MetricValue { get; set; }

        /// <summary>
        /// Gets or sets the alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets alert details.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();

        /// <summary>
        /// Gets or sets the alert state.
        /// </summary>
        public AlertState State { get; set; }

        /// <summary>
        /// Gets or sets who acknowledged the alert.
        /// </summary>
        public string? AcknowledgedBy { get; set; }

        /// <summary>
        /// Gets or sets when the alert was acknowledged.
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>
        /// Gets or sets acknowledgment notes.
        /// </summary>
        public string? AcknowledgmentNotes { get; set; }

        /// <summary>
        /// Gets or sets when the alert was resolved.
        /// </summary>
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>
    /// Alert states.
    /// </summary>
    public enum AlertState
    {
        /// <summary>
        /// Alert is active.
        /// </summary>
        Active,

        /// <summary>
        /// Alert has been acknowledged.
        /// </summary>
        Acknowledged,

        /// <summary>
        /// Alert has been resolved.
        /// </summary>
        Resolved,

        /// <summary>
        /// Alert was suppressed.
        /// </summary>
        Suppressed
    }

    /// <summary>
    /// Alert test result.
    /// </summary>
    public class AlertTestResult
    {
        /// <summary>
        /// Gets or sets whether the test passed.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the test message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the condition would trigger.
        /// </summary>
        public bool WouldTrigger { get; set; }

        /// <summary>
        /// Gets or sets simulated metric value.
        /// </summary>
        public double SimulatedMetricValue { get; set; }

        /// <summary>
        /// Gets or sets notification test results.
        /// </summary>
        public List<NotificationTestResult> NotificationTests { get; set; } = new();
    }

    /// <summary>
    /// Notification test result.
    /// </summary>
    public class NotificationTestResult
    {
        /// <summary>
        /// Gets or sets the channel type.
        /// </summary>
        public NotificationChannelType ChannelType { get; set; }

        /// <summary>
        /// Gets or sets whether the test succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
