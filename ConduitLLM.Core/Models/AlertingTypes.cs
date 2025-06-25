using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Alert condition specification.
    /// </summary>
    public class AlertCondition
    {
        public ComparisonOperator Operator { get; set; }
        public double Threshold { get; set; }
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(5);
        public int MinimumOccurrences { get; set; } = 1;
        public string? Provider { get; set; } // Optional provider filter
        public string? Model { get; set; } // Optional model filter
        public string? ProviderFilter { get; set; } // Legacy field for compatibility
        public string? CustomExpression { get; set; } // For custom expressions
    }

    /// <summary>
    /// Comparison operators for alert conditions.
    /// </summary>
    public enum ComparisonOperator
    {
        GreaterThan,
        LessThan,
        Equals,
        NotEquals,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    /// <summary>
    /// Alert severity levels.
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Notification channel configuration.
    /// </summary>
    public class NotificationChannel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public NotificationChannelType Type { get; set; }
        public string Target { get; set; } = string.Empty; // Email, webhook URL, etc.
        public Dictionary<string, string> Configuration { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public List<AlertSeverity> SeverityFilter { get; set; } = new();
    }

    /// <summary>
    /// Types of notification channels.
    /// </summary>
    public enum NotificationChannelType
    {
        Email,
        Webhook,
        Slack,
        Teams,
        PagerDuty,
        SMS
    }

    /// <summary>
    /// Result of an alert rule test.
    /// </summary>
    public class AlertTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool WouldTrigger { get; set; }
        public double SimulatedMetricValue { get; set; }
        public List<NotificationTestResult> NotificationTests { get; set; } = new();
    }

    /// <summary>
    /// Result of a notification channel test.
    /// </summary>
    public class NotificationTestResult
    {
        public string ChannelId { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public NotificationChannelType ChannelType { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double ResponseTimeMs { get; set; }
    }

    /// <summary>
    /// Result of sending a notification.
    /// </summary>
    public class NotificationResult
    {
        public string ChannelId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime SentAt { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Alert states.
    /// </summary>
    public enum AlertState
    {
        Active,
        Acknowledged,
        Resolved,
        Expired
    }
}