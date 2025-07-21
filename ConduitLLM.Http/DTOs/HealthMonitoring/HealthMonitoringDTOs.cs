using System;
using System.Collections.Generic;

namespace ConduitLLM.Http.DTOs.HealthMonitoring
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

    /// <summary>
    /// Component health status
    /// </summary>
    public class ComponentHealth
    {
        /// <summary>
        /// Component name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Last check timestamp
        /// </summary>
        public DateTime LastCheck { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Error message if unhealthy
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Additional health metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();

        /// <summary>
        /// Dependencies health status
        /// </summary>
        public List<ComponentHealth> Dependencies { get; set; } = new();
    }

    /// <summary>
    /// System health snapshot
    /// </summary>
    public class SystemHealthSnapshot
    {
        /// <summary>
        /// Snapshot timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Overall system health
        /// </summary>
        public HealthStatus OverallStatus { get; set; }

        /// <summary>
        /// Individual component health statuses
        /// </summary>
        public List<ComponentHealth> Components { get; set; } = new();

        /// <summary>
        /// Active alerts
        /// </summary>
        public List<HealthAlert> ActiveAlerts { get; set; } = new();

        /// <summary>
        /// System resource metrics
        /// </summary>
        public ResourceMetrics Resources { get; set; } = new();

        /// <summary>
        /// Performance metrics
        /// </summary>
        public PerformanceMetrics Performance { get; set; } = new();
    }

    /// <summary>
    /// Resource utilization metrics
    /// </summary>
    public class ResourceMetrics
    {
        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// Disk usage percentage
        /// </summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>
        /// Network I/O in MB/s
        /// </summary>
        public double NetworkIOMBps { get; set; }

        /// <summary>
        /// Thread count
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Connection pool stats
        /// </summary>
        public ConnectionPoolStats ConnectionPools { get; set; } = new();
    }

    /// <summary>
    /// Connection pool statistics
    /// </summary>
    public class ConnectionPoolStats
    {
        /// <summary>
        /// Database connection pool
        /// </summary>
        public PoolStats Database { get; set; } = new();

        /// <summary>
        /// Redis connection pool
        /// </summary>
        public PoolStats Redis { get; set; } = new();

        /// <summary>
        /// HTTP client connections
        /// </summary>
        public PoolStats HttpClients { get; set; } = new();
    }

    /// <summary>
    /// Individual pool statistics
    /// </summary>
    public class PoolStats
    {
        /// <summary>
        /// Active connections
        /// </summary>
        public int Active { get; set; }

        /// <summary>
        /// Idle connections
        /// </summary>
        public int Idle { get; set; }

        /// <summary>
        /// Maximum pool size
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Pool utilization percentage
        /// </summary>
        public double UtilizationPercent { get; set; }

        /// <summary>
        /// Wait queue length
        /// </summary>
        public int WaitQueueLength { get; set; }
    }

    /// <summary>
    /// Performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Request rate per second
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Average response time
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// P95 response time
        /// </summary>
        public double P95ResponseTimeMs { get; set; }

        /// <summary>
        /// P99 response time
        /// </summary>
        public double P99ResponseTimeMs { get; set; }

        /// <summary>
        /// Error rate percentage
        /// </summary>
        public double ErrorRatePercent { get; set; }

        /// <summary>
        /// Active request count
        /// </summary>
        public int ActiveRequests { get; set; }

        /// <summary>
        /// Queue depth
        /// </summary>
        public int QueueDepth { get; set; }
    }

    /// <summary>
    /// Alert rule configuration
    /// </summary>
    public class AlertRule
    {
        /// <summary>
        /// Rule identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Rule name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Rule description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Component to monitor
        /// </summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// Metric to evaluate
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Comparison operator
        /// </summary>
        public ComparisonOperator Operator { get; set; }

        /// <summary>
        /// Threshold value
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Alert severity
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert type
        /// </summary>
        public AlertType AlertType { get; set; }

        /// <summary>
        /// Evaluation window in seconds
        /// </summary>
        public int EvaluationWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Minimum occurrences before alerting
        /// </summary>
        public int MinOccurrences { get; set; } = 1;

        /// <summary>
        /// Is rule enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Suppression period after alert in minutes
        /// </summary>
        public int SuppressionMinutes { get; set; } = 5;
    }

    /// <summary>
    /// Alert suppression configuration
    /// </summary>
    public class AlertSuppression
    {
        /// <summary>
        /// Suppression rule ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Alert pattern to suppress
        /// </summary>
        public string AlertPattern { get; set; } = string.Empty;

        /// <summary>
        /// Start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Reason for suppression
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Created by user
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Health check result
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Check name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Check duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Tags
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Additional data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Error description if unhealthy
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Exception details if failed
        /// </summary>
        public string? Exception { get; set; }
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Alert types
    /// </summary>
    public enum AlertType
    {
        ServiceDown,
        ServiceDegraded,
        PerformanceDegradation,
        ResourceExhaustion,
        SecurityEvent,
        ConfigurationChange,
        ThresholdBreach,
        ConnectivityIssue,
        DataIntegrity,
        Custom
    }

    /// <summary>
    /// Alert states
    /// </summary>
    public enum AlertState
    {
        Active,
        Acknowledged,
        Resolved,
        Suppressed,
        Expired
    }

    /// <summary>
    /// Health status
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }

    /// <summary>
    /// Comparison operators for alert rules
    /// </summary>
    public enum ComparisonOperator
    {
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equal,
        NotEqual
    }

    /// <summary>
    /// Alert notification request
    /// </summary>
    public class AlertNotificationRequest
    {
        /// <summary>
        /// Alert to send
        /// </summary>
        public HealthAlert Alert { get; set; } = new();

        /// <summary>
        /// Notification channels
        /// </summary>
        public List<string> Channels { get; set; } = new();

        /// <summary>
        /// Additional recipients
        /// </summary>
        public List<string> Recipients { get; set; } = new();
    }

    /// <summary>
    /// Alert history entry
    /// </summary>
    public class AlertHistoryEntry
    {
        /// <summary>
        /// Entry ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Alert ID
        /// </summary>
        public string AlertId { get; set; } = string.Empty;

        /// <summary>
        /// Action taken
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// User who took action
        /// </summary>
        public string? User { get; set; }

        /// <summary>
        /// Action timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional notes
        /// </summary>
        public string? Notes { get; set; }
    }
}