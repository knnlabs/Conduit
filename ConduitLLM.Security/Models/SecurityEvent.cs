using System;
using ConduitLLM.Configuration.DTOs.Security;

namespace ConduitLLM.Security.Models
{
    /// <summary>
    /// Internal model for security event
    /// </summary>
    internal class SecurityEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SecurityEventType EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? VirtualKey { get; set; }
        public string? Endpoint { get; set; }
        public string? Activity { get; set; }
        public string? Details { get; set; }
        public long? DataSize { get; set; }
    }

    /// <summary>
    /// IP activity profile for tracking
    /// </summary>
    internal class IpActivityProfile
    {
        public string IpAddress { get; set; } = string.Empty;
        public int AuthenticationFailures { get; set; }
        public int AuthenticationSuccesses { get; set; }
        public int RateLimitViolations { get; set; }
        public int SuspiciousActivities { get; set; }
        public int DataExfiltrationAttempts { get; set; }
        public int AnomalousActivities { get; set; }
        public bool IsBanned { get; set; }
        public DateTime? BannedAt { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public DateTime? LastSuccessfulAuth { get; set; }
    }

    /// <summary>
    /// Virtual key activity profile for tracking
    /// </summary>
    internal class VirtualKeyActivityProfile
    {
        public string VirtualKey { get; set; } = string.Empty;
        public int AuthenticationFailures { get; set; }
        public int AuthenticationSuccesses { get; set; }
        public int RateLimitViolations { get; set; }
        public int DataExfiltrationAttempts { get; set; }
        public int AnomalousActivities { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// State for anomaly detection
    /// </summary>
    internal class AnomalyDetectionState
    {
        public string Identifier { get; set; } = string.Empty;
        public Dictionary<string, int> EndpointAccess { get; set; } = new();
        public DateTime WindowStart { get; set; }
        public int TotalRequests { get; set; }
    }

    /// <summary>
    /// Security monitoring configuration options
    /// </summary>
    public class SecurityMonitoringOptions
    {
        // Event retention
        public int MaxEventsRetention { get; set; } = 100000;
        public int DataRetentionHours { get; set; } = 24;

        // Analysis intervals
        public int AnalysisIntervalSeconds { get; set; } = 60;
        public int MetricsWindowMinutes { get; set; } = 60;

        // Brute force detection
        public int BruteForceThreshold { get; set; } = 10;
        public int BruteForceDetectionWindowMinutes { get; set; } = 10;

        // Distributed attack detection
        public int DistributedAttackIpThreshold { get; set; } = 5;
        public int DistributedAttackWindowMinutes { get; set; } = 30;

        // Anomaly detection
        public int AnomalousEndpointThreshold { get; set; } = 20;
        public int AnomalousRequestRateMultiplier { get; set; } = 10;

        // Data exfiltration detection
        public long DataExfiltrationSizeThreshold { get; set; } = 100_000_000; // 100MB
        public int DataExfiltrationRequestThreshold { get; set; } = 100;
    }
}