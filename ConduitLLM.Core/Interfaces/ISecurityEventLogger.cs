using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for logging security-related events.
    /// </summary>
    /// <remarks>
    /// This interface provides a centralized way to log security events such as authentication failures,
    /// authorization violations, rate limit breaches, and suspicious activities. These logs can be used
    /// for security monitoring, alerting, and compliance auditing.
    /// </remarks>
    public interface ISecurityEventLogger
    {
        /// <summary>
        /// Logs an authentication failure event.
        /// </summary>
        /// <param name="virtualKey">The virtual key that failed authentication.</param>
        /// <param name="ipAddress">The IP address from which the request originated.</param>
        /// <param name="reason">Optional reason for the authentication failure.</param>
        /// <param name="additionalContext">Optional additional context data.</param>
        Task LogAuthenticationFailureAsync(
            string virtualKey, 
            string ipAddress, 
            string? reason = null,
            Dictionary<string, object>? additionalContext = null);

        /// <summary>
        /// Logs a successful authentication event.
        /// </summary>
        /// <param name="virtualKey">The virtual key that authenticated successfully.</param>
        /// <param name="ipAddress">The IP address from which the request originated.</param>
        /// <param name="additionalContext">Optional additional context data.</param>
        Task LogAuthenticationSuccessAsync(
            string virtualKey, 
            string ipAddress,
            Dictionary<string, object>? additionalContext = null);

        /// <summary>
        /// Logs when rate limits are exceeded.
        /// </summary>
        /// <param name="virtualKey">The virtual key that exceeded rate limits.</param>
        /// <param name="endpoint">The endpoint that was rate limited.</param>
        /// <param name="limit">The rate limit that was exceeded.</param>
        /// <param name="window">The time window for the rate limit.</param>
        /// <param name="ipAddress">The IP address from which the request originated.</param>
        Task LogRateLimitExceededAsync(
            string virtualKey, 
            string endpoint, 
            int limit, 
            TimeSpan window,
            string ipAddress);

        /// <summary>
        /// Logs suspicious activity that may indicate a security threat.
        /// </summary>
        /// <param name="description">Description of the suspicious activity.</param>
        /// <param name="severity">Severity level (Low, Medium, High, Critical).</param>
        /// <param name="context">Context data including relevant details.</param>
        Task LogSuspiciousActivityAsync(
            string description, 
            SecurityEventSeverity severity,
            Dictionary<string, object> context);

        /// <summary>
        /// Logs authorization violations.
        /// </summary>
        /// <param name="virtualKey">The virtual key that attempted unauthorized access.</param>
        /// <param name="resource">The resource that was attempted to be accessed.</param>
        /// <param name="action">The action that was attempted.</param>
        /// <param name="ipAddress">The IP address from which the request originated.</param>
        Task LogAuthorizationViolationAsync(
            string virtualKey,
            string resource,
            string action,
            string ipAddress);

        /// <summary>
        /// Logs IP filtering events.
        /// </summary>
        /// <param name="ipAddress">The IP address that was filtered.</param>
        /// <param name="action">The action taken (Blocked, Allowed).</param>
        /// <param name="reason">The reason for the action.</param>
        /// <param name="virtualKey">Optional virtual key associated with the request.</param>
        Task LogIpFilteringEventAsync(
            string ipAddress,
            IpFilterAction action,
            string reason,
            string? virtualKey = null);

        /// <summary>
        /// Logs data validation failures that might indicate attack attempts.
        /// </summary>
        /// <param name="endpoint">The endpoint where validation failed.</param>
        /// <param name="fieldName">The field that failed validation.</param>
        /// <param name="invalidValue">The invalid value (sanitized).</param>
        /// <param name="ipAddress">The IP address from which the request originated.</param>
        /// <param name="virtualKey">Optional virtual key associated with the request.</param>
        Task LogValidationFailureAsync(
            string endpoint,
            string fieldName,
            string invalidValue,
            string ipAddress,
            string? virtualKey = null);

        /// <summary>
        /// Logs API key rotation events.
        /// </summary>
        /// <param name="virtualKey">The virtual key that was rotated.</param>
        /// <param name="reason">The reason for rotation.</param>
        /// <param name="performedBy">Who performed the rotation (user/system).</param>
        Task LogApiKeyRotationAsync(
            string virtualKey,
            string reason,
            string performedBy);

        /// <summary>
        /// Logs security configuration changes.
        /// </summary>
        /// <param name="setting">The security setting that was changed.</param>
        /// <param name="oldValue">The previous value.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="changedBy">Who made the change.</param>
        Task LogSecurityConfigurationChangeAsync(
            string setting,
            string oldValue,
            string newValue,
            string changedBy);

        /// <summary>
        /// Gets security events for a time period.
        /// </summary>
        /// <param name="startTime">Start time for the query.</param>
        /// <param name="endTime">End time for the query.</param>
        /// <param name="eventTypes">Optional filter for specific event types.</param>
        /// <param name="severity">Optional minimum severity filter.</param>
        /// <returns>Collection of security events.</returns>
        Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(
            DateTime startTime,
            DateTime endTime,
            SecurityEventType[]? eventTypes = null,
            SecurityEventSeverity? severity = null);

        /// <summary>
        /// Gets security event statistics.
        /// </summary>
        /// <param name="startTime">Start time for statistics.</param>
        /// <param name="endTime">End time for statistics.</param>
        /// <returns>Security event statistics.</returns>
        Task<SecurityEventStatistics> GetStatisticsAsync(
            DateTime startTime,
            DateTime endTime);
    }

    /// <summary>
    /// Represents a security event.
    /// </summary>
    public class SecurityEvent
    {
        /// <summary>
        /// Gets or sets the event ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public SecurityEventType EventType { get; set; }

        /// <summary>
        /// Gets or sets the severity.
        /// </summary>
        public SecurityEventSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual key involved.
        /// </summary>
        public string? VirtualKey { get; set; }

        /// <summary>
        /// Gets or sets the IP address.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Gets or sets additional context data.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();

        /// <summary>
        /// Gets or sets the correlation ID for tracing.
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Types of security events.
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// Authentication failure.
        /// </summary>
        AuthenticationFailure,

        /// <summary>
        /// Successful authentication.
        /// </summary>
        AuthenticationSuccess,

        /// <summary>
        /// Authorization violation.
        /// </summary>
        AuthorizationViolation,

        /// <summary>
        /// Rate limit exceeded.
        /// </summary>
        RateLimitExceeded,

        /// <summary>
        /// IP filtering event.
        /// </summary>
        IpFiltering,

        /// <summary>
        /// Suspicious activity detected.
        /// </summary>
        SuspiciousActivity,

        /// <summary>
        /// Validation failure.
        /// </summary>
        ValidationFailure,

        /// <summary>
        /// API key rotation.
        /// </summary>
        ApiKeyRotation,

        /// <summary>
        /// Security configuration change.
        /// </summary>
        ConfigurationChange
    }

    /// <summary>
    /// Severity levels for security events.
    /// </summary>
    public enum SecurityEventSeverity
    {
        /// <summary>
        /// Low severity - informational.
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity - worth investigating.
        /// </summary>
        Medium,

        /// <summary>
        /// High severity - requires attention.
        /// </summary>
        High,

        /// <summary>
        /// Critical severity - immediate action required.
        /// </summary>
        Critical
    }

    /// <summary>
    /// IP filtering actions.
    /// </summary>
    public enum IpFilterAction
    {
        /// <summary>
        /// IP was blocked.
        /// </summary>
        Blocked,

        /// <summary>
        /// IP was allowed.
        /// </summary>
        Allowed,

        /// <summary>
        /// IP was rate limited.
        /// </summary>
        RateLimited
    }

    /// <summary>
    /// Security event statistics.
    /// </summary>
    public class SecurityEventStatistics
    {
        /// <summary>
        /// Gets or sets the time period.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets total events.
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// Gets or sets events by type.
        /// </summary>
        public Dictionary<SecurityEventType, int> EventsByType { get; set; } = new();

        /// <summary>
        /// Gets or sets events by severity.
        /// </summary>
        public Dictionary<SecurityEventSeverity, int> EventsBySeverity { get; set; } = new();

        /// <summary>
        /// Gets or sets top IP addresses by event count.
        /// </summary>
        public List<(string IpAddress, int Count)> TopIpAddresses { get; set; } = new();

        /// <summary>
        /// Gets or sets top virtual keys by event count.
        /// </summary>
        public List<(string VirtualKey, int Count)> TopVirtualKeys { get; set; } = new();

        /// <summary>
        /// Gets or sets authentication failure rate.
        /// </summary>
        public double AuthenticationFailureRate { get; set; }

        /// <summary>
        /// Gets or sets rate limit violation count.
        /// </summary>
        public int RateLimitViolations { get; set; }
    }
}