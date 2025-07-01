using System;

namespace ConduitLLM.Configuration.DTOs.Security
{
    /// <summary>
    /// Represents a security event in the system
    /// </summary>
    public class SecurityEventDto
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of security event
        /// </summary>
        public SecurityEventType EventType { get; set; }

        /// <summary>
        /// When the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// IP address associated with the event
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Virtual key associated with the event (if applicable)
        /// </summary>
        public string? VirtualKey { get; set; }

        /// <summary>
        /// API endpoint involved in the event
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Activity description for suspicious activity events
        /// </summary>
        public string? Activity { get; set; }

        /// <summary>
        /// Additional details about the event
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Data size for exfiltration events
        /// </summary>
        public long? DataSize { get; set; }

        /// <summary>
        /// Severity of the event
        /// </summary>
        public SecurityEventSeverity Severity { get; set; }

        /// <summary>
        /// Whether the event has been acknowledged
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// User who acknowledged the event
        /// </summary>
        public string? AcknowledgedBy { get; set; }

        /// <summary>
        /// When the event was acknowledged
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }
    }

    /// <summary>
    /// Types of security events
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// Failed authentication attempt
        /// </summary>
        AuthenticationFailure,

        /// <summary>
        /// Successful authentication
        /// </summary>
        AuthenticationSuccess,

        /// <summary>
        /// Rate limit exceeded
        /// </summary>
        RateLimitViolation,

        /// <summary>
        /// Suspicious activity detected
        /// </summary>
        SuspiciousActivity,

        /// <summary>
        /// Potential data exfiltration
        /// </summary>
        DataExfiltration,

        /// <summary>
        /// Anomalous access pattern
        /// </summary>
        AnomalousAccess,

        /// <summary>
        /// IP address banned
        /// </summary>
        IpBanned,

        /// <summary>
        /// Authorization violation
        /// </summary>
        AuthorizationViolation,

        /// <summary>
        /// Security configuration changed
        /// </summary>
        SecurityConfigurationChange,

        /// <summary>
        /// Malicious payload detected
        /// </summary>
        MaliciousPayloadDetected
    }

    /// <summary>
    /// Security event severity levels
    /// </summary>
    public enum SecurityEventSeverity
    {
        /// <summary>
        /// Informational event
        /// </summary>
        Info,

        /// <summary>
        /// Low severity event
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity event
        /// </summary>
        Medium,

        /// <summary>
        /// High severity event
        /// </summary>
        High,

        /// <summary>
        /// Critical severity event
        /// </summary>
        Critical
    }
}