using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    // Mirror DTOs from ConduitLLM.Http.DTOs.HealthMonitoring for WebUI usage
    
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public enum AlertType
    {
        ServiceDown,
        ConnectivityIssue,
        PerformanceDegradation,
        ResourceExhaustion,
        SecurityEvent,
        ConfigurationError,
        Custom
    }

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }

    public enum SecurityEventType
    {
        AuthenticationFailure,
        RateLimitViolation,
        SuspiciousActivity,
        DataExfiltrationAttempt,
        UnauthorizedAccess
    }

    public enum AlertState
    {
        Active,
        Acknowledged,
        Suppressed,
        Resolved
    }

    public class HealthAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public AlertSeverity Severity { get; set; }
        public AlertType Type { get; set; }
        public string Component { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
        public List<string> SuggestedActions { get; set; } = new();
        public bool IsSuppressed { get; set; }
        public string? SuppressionReason { get; set; }
        public int OccurrenceCount { get; set; } = 1;
        public AlertState State { get; set; } = AlertState.Active;
        public bool IsAcknowledged => AcknowledgedAt.HasValue;
    }

    public class SystemHealthSnapshot
    {
        public HealthStatus OverallStatus { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, ComponentHealth> Components { get; set; } = new();
        public List<HealthAlert> ActiveAlerts { get; set; } = new();
        public PerformanceMetrics Performance { get; set; } = new();
        public ResourceUsage Resources { get; set; } = new();
    }

    public class ComponentHealth
    {
        public string Name { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public string? StatusMessage { get; set; }
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<HealthCheckResult> Checks { get; set; } = new();
    }

    public class HealthCheckResult
    {
        public string Name { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public string? Description { get; set; }
        public double? DurationMs { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class PerformanceMetrics
    {
        public double AverageResponseTimeMs { get; set; }
        public double P95ResponseTimeMs { get; set; }
        public double P99ResponseTimeMs { get; set; }
        public double RequestsPerSecond { get; set; }
        public double ErrorRate { get; set; }
        public double ErrorRatePercent => ErrorRate * 100;
        public double CacheHitRate { get; set; }
        public Dictionary<string, EndpointMetrics> EndpointMetrics { get; set; } = new();
    }

    public class EndpointMetrics
    {
        public string Endpoint { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double ErrorRate { get; set; }
    }

    public class ResourceUsage
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long MemoryTotalBytes { get; set; }
        public double DiskUsagePercent { get; set; }
        public long DiskUsedBytes { get; set; }
        public long DiskTotalBytes { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long MemoryUsageMB => MemoryUsedBytes / (1024 * 1024);
        public long DiskFreeMB => (DiskTotalBytes - DiskUsedBytes) / (1024 * 1024);
        public Dictionary<string, ConnectionPoolStatus> ConnectionPools { get; set; } = new();
    }

    public class ConnectionPoolStatus
    {
        public string Name { get; set; } = string.Empty;
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int MaxConnections { get; set; }
        public double UsagePercent { get; set; }
    }

    public class AlertRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public AlertType AlertType { get; set; }
        public string Condition { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int CooldownMinutes { get; set; } = 5;
        public DateTime? LastTriggered { get; set; }
    }

    public class AlertSuppression
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AlertPattern { get; set; } = string.Empty;
        public string? Component { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AlertHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AlertId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? User { get; set; }
        public string? Notes { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SecurityMetrics
    {
        public int AuthenticationFailures { get; set; }
        public int RateLimitViolations { get; set; }
        public int SuspiciousActivities { get; set; }
        public int ActiveIpBans { get; set; }
        public int DataExfiltrationAttempts { get; set; }
        public int UniqueKeysMonitored { get; set; }
        public int UniqueIpsMonitored { get; set; }
        public int AnomalousAccessPatterns { get; set; }
        public Models.ThreatLevel ThreatLevel { get; set; }
        public List<SecurityEvent> RecentEvents { get; set; } = new();
        public Dictionary<string, int> EventsByType { get; set; } = new();
    }

    public class SecurityEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SecurityEventType EventType { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? VirtualKey { get; set; }
        public string? Endpoint { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Context { get; set; } = new();
        public Dictionary<string, string> Details { get; set; } = new();
        public bool IsBlocked { get; set; }
    }
}