using System;
using System.Collections.Generic;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Response containing recent security events.
    /// </summary>
    public class SecurityEventsResponse
    {
        /// <summary>
        /// Timestamp when the response was generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Time range analyzed for security events.
        /// </summary>
        public TimeRangeDto TimeRange { get; set; } = new();

        /// <summary>
        /// Total number of security events returned.
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// Event counts grouped by event type.
        /// </summary>
        public List<SecurityEventTypeCountDto> EventsByType { get; set; } = new();

        /// <summary>
        /// Event counts grouped by severity.
        /// </summary>
        public List<SecurityEventSeverityCountDto> EventsBySeverity { get; set; } = new();

        /// <summary>
        /// The individual security events, most recent first.
        /// </summary>
        public List<SecurityMonitoringEventDto> Events { get; set; } = new();
    }

    /// <summary>
    /// Number of security events of a given type.
    /// </summary>
    public class SecurityEventTypeCountDto
    {
        /// <summary>
        /// Security event type identifier.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Number of events of this type.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Number of security events of a given severity.
    /// </summary>
    public class SecurityEventSeverityCountDto
    {
        /// <summary>
        /// Event severity (warning or high).
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Number of events with this severity.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// A single security event derived from request log analysis.
    /// </summary>
    public class SecurityMonitoringEventDto
    {
        /// <summary>
        /// When the event occurred (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Event type (auth_failure, rate_limit, blocked_ip, or suspicious_activity).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Event severity (warning or high).
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Source IP address of the event.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the virtual key involved, if applicable.
        /// </summary>
        public string? VirtualKeyId { get; set; }

        /// <summary>
        /// Human-readable event description.
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code associated with the event, if applicable.
        /// </summary>
        public int? StatusCode { get; set; }
    }

    /// <summary>
    /// Response containing threat analytics data.
    /// </summary>
    public class ThreatAnalyticsResponse
    {
        /// <summary>
        /// Timestamp when the analytics were generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Aggregate security metrics.
        /// </summary>
        public ThreatAnalyticsMetricsDto Metrics { get; set; } = new();

        /// <summary>
        /// Highest-risk threat sources, ordered by risk score.
        /// </summary>
        public List<TopThreatSourceDto> TopThreats { get; set; } = new();

        /// <summary>
        /// Threat counts grouped by threat type.
        /// </summary>
        public List<ThreatDistributionDto> ThreatDistribution { get; set; } = new();

        /// <summary>
        /// Daily threat counts over the analyzed period.
        /// </summary>
        public List<ThreatTrendPointDto> ThreatTrend { get; set; } = new();
    }

    /// <summary>
    /// Aggregate security metrics for threat analytics.
    /// </summary>
    public class ThreatAnalyticsMetricsDto
    {
        /// <summary>
        /// Total number of threats detected today.
        /// </summary>
        public int TotalThreatsToday { get; set; }

        /// <summary>
        /// Number of unique threat source IPs detected today.
        /// </summary>
        public int UniqueThreatsToday { get; set; }

        /// <summary>
        /// Number of IP addresses currently blocked.
        /// </summary>
        public int BlockedIPs { get; set; }

        /// <summary>
        /// Overall compliance score percentage.
        /// </summary>
        public double ComplianceScore { get; set; }
    }

    /// <summary>
    /// A threat source ranked by risk score.
    /// </summary>
    public class TopThreatSourceDto
    {
        /// <summary>
        /// IP address of the threat source.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Total number of failed requests from this source.
        /// </summary>
        public int TotalFailures { get; set; }

        /// <summary>
        /// Number of distinct days this source was active.
        /// </summary>
        public int DaysActive { get; set; }

        /// <summary>
        /// Date this source was last seen (UTC).
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Calculated risk score (failures per active day).
        /// </summary>
        public double RiskScore { get; set; }
    }

    /// <summary>
    /// Threat counts for a single threat type.
    /// </summary>
    public class ThreatDistributionDto
    {
        /// <summary>
        /// Threat type (Authentication, Authorization, RateLimit, InvalidRequest, or Other).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Number of threats of this type.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Number of unique source IPs for this threat type.
        /// </summary>
        public int UniqueIPs { get; set; }
    }

    /// <summary>
    /// Threat count for a single day.
    /// </summary>
    public class ThreatTrendPointDto
    {
        /// <summary>
        /// The day the threats occurred (UTC).
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Number of threats detected on this day.
        /// </summary>
        public int Threats { get; set; }
    }

    /// <summary>
    /// Response containing compliance metrics.
    /// </summary>
    public class ComplianceMetricsResponse
    {
        /// <summary>
        /// Timestamp when the metrics were generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Data protection compliance details.
        /// </summary>
        public DataProtectionDto DataProtection { get; set; } = new();

        /// <summary>
        /// Access control compliance details.
        /// </summary>
        public AccessControlDto AccessControl { get; set; } = new();

        /// <summary>
        /// Monitoring compliance details.
        /// </summary>
        public ComplianceMonitoringDto Monitoring { get; set; } = new();

        /// <summary>
        /// Overall compliance score percentage.
        /// </summary>
        public double ComplianceScore { get; set; }
    }

    /// <summary>
    /// Data protection compliance details.
    /// </summary>
    public class DataProtectionDto
    {
        /// <summary>
        /// Number of enabled (encrypted) virtual keys.
        /// </summary>
        public int EncryptedKeys { get; set; }

        /// <summary>
        /// Whether endpoints are secured with HTTPS.
        /// </summary>
        public bool SecureEndpoints { get; set; }

        /// <summary>
        /// Data retention period in days.
        /// </summary>
        public int DataRetentionDays { get; set; }

        /// <summary>
        /// Date of the last data protection audit (UTC).
        /// </summary>
        public DateTime LastAudit { get; set; }
    }

    /// <summary>
    /// Access control compliance details.
    /// </summary>
    public class AccessControlDto
    {
        /// <summary>
        /// Number of active virtual keys.
        /// </summary>
        public int ActiveKeys { get; set; }

        /// <summary>
        /// Number of key groups with a positive balance (budget controls).
        /// </summary>
        public int KeysWithBudgets { get; set; }

        /// <summary>
        /// Whether IP whitelisting is enabled.
        /// </summary>
        public bool IpWhitelistEnabled { get; set; }

        /// <summary>
        /// Whether rate limiting is enabled.
        /// </summary>
        public bool RateLimitingEnabled { get; set; }
    }

    /// <summary>
    /// Monitoring compliance details.
    /// </summary>
    public class ComplianceMonitoringDto
    {
        /// <summary>
        /// Log retention period in days.
        /// </summary>
        public int LogRetentionDays { get; set; }

        /// <summary>
        /// Whether request logging is enabled.
        /// </summary>
        public bool RequestLoggingEnabled { get; set; }

        /// <summary>
        /// Whether security alerts are enabled.
        /// </summary>
        public bool SecurityAlertsEnabled { get; set; }

        /// <summary>
        /// Date of the last security review (UTC).
        /// </summary>
        public DateTime LastSecurityReview { get; set; }
    }
}
