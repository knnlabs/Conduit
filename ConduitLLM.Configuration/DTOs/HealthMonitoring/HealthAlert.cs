using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// System health alert notification
    /// </summary>
    public class HealthAlert
    {
        /// <summary>
        /// Unique identifier for the alert
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Alert severity level
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert type category
        /// </summary>
        public AlertType Type { get; set; }

        /// <summary>
        /// Component or service name
        /// </summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// Alert title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed alert message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// When the alert was triggered
        /// </summary>
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the alert was resolved (if applicable)
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Current alert state
        /// </summary>
        public AlertState State { get; set; } = AlertState.Active;

        /// <summary>
        /// Additional context data
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();

        /// <summary>
        /// Number of times this alert has been triggered
        /// </summary>
        public int OccurrenceCount { get; set; } = 1;

        /// <summary>
        /// Last time this alert was updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the alert has been acknowledged
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// User who acknowledged the alert
        /// </summary>
        public string? AcknowledgedBy { get; set; }

        /// <summary>
        /// When the alert was acknowledged
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>
        /// Suggested actions to resolve the alert
        /// </summary>
        public List<string> SuggestedActions { get; set; } = new();
    }
}