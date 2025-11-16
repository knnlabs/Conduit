using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for monitoring health and accuracy of distributed cache statistics.
    /// </summary>
    public interface IStatisticsHealthCheck
    {
        /// <summary>
        /// Performs a comprehensive health check of the statistics system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check result with detailed status information.</returns>
        Task<StatisticsHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the accuracy of statistics across all instances.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Accuracy report with any detected discrepancies.</returns>
        Task<StatisticsAccuracyReport> ValidateAccuracyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets performance metrics for the statistics system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Performance metrics including latencies and throughput.</returns>
        Task<StatisticsPerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Configures alerting thresholds for monitoring.
        /// </summary>
        /// <param name="thresholds">Alert threshold configuration.</param>
        Task ConfigureAlertingAsync(StatisticsAlertThresholds thresholds);

        /// <summary>
        /// Gets currently active monitoring alerts.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of active alerts.</returns>
        Task<IEnumerable<StatisticsMonitoringAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when a monitoring alert is triggered.
        /// </summary>
        event EventHandler<StatisticsMonitoringAlertEventArgs>? AlertTriggered;
    }

    /// <summary>
    /// Result of a statistics system health check.
    /// </summary>
    public class StatisticsHealthCheckResult
    {
        /// <summary>
        /// Overall health status.
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Indicates if Redis connectivity is healthy.
        /// </summary>
        public bool RedisConnected { get; set; }

        /// <summary>
        /// Number of active statistics collector instances.
        /// </summary>
        public int ActiveInstances { get; set; }

        /// <summary>
        /// Number of instances not reporting (missing heartbeat).
        /// </summary>
        public int MissingInstances { get; set; }

        /// <summary>
        /// Average aggregation latency in milliseconds.
        /// </summary>
        public double AggregationLatencyMs { get; set; }

        /// <summary>
        /// Redis memory usage for statistics in bytes.
        /// </summary>
        public long RedisMemoryUsageBytes { get; set; }

        /// <summary>
        /// Last successful aggregation timestamp.
        /// </summary>
        public DateTime? LastSuccessfulAggregation { get; set; }

        /// <summary>
        /// Detailed health check messages.
        /// </summary>
        public List<string> Messages { get; set; } = new();

        /// <summary>
        /// Component-specific health statuses.
        /// </summary>
        public Dictionary<string, ComponentHealth> ComponentHealth { get; set; } = new();
    }

    /// <summary>
    /// Health status enumeration.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// All systems operating normally.
        /// </summary>
        Healthy,

        /// <summary>
        /// System operational but with warnings.
        /// </summary>
        Degraded,

        /// <summary>
        /// System is not functioning properly.
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Component-specific health information.
    /// </summary>
    public class ComponentHealth
    {
        /// <summary>
        /// Component name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Component health status.
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Component-specific message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Last check timestamp.
        /// </summary>
        public DateTime LastCheck { get; set; }
    }

    /// <summary>
    /// Report on statistics accuracy across instances.
    /// </summary>
    public class StatisticsAccuracyReport
    {
        /// <summary>
        /// Indicates if statistics are accurate within tolerance.
        /// </summary>
        public bool IsAccurate { get; set; }

        /// <summary>
        /// Maximum drift percentage detected between instances.
        /// </summary>
        public double MaxDriftPercentage { get; set; }

        /// <summary>
        /// Regions with detected discrepancies.
        /// </summary>
        public List<RegionDiscrepancy> Discrepancies { get; set; } = new();

        /// <summary>
        /// Instances with inconsistent data.
        /// </summary>
        public List<string> InconsistentInstances { get; set; } = new();

        /// <summary>
        /// Timestamp of accuracy check.
        /// </summary>
        public DateTime CheckTimestamp { get; set; }

        /// <summary>
        /// Time taken to perform accuracy check.
        /// </summary>
        public TimeSpan CheckDuration { get; set; }
    }

    /// <summary>
    /// Discrepancy detected in a cache region.
    /// </summary>
    public class RegionDiscrepancy
    {
        /// <summary>
        /// Cache region with discrepancy.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Type of discrepancy detected.
        /// </summary>
        public DiscrepancyType Type { get; set; }

        /// <summary>
        /// Expected value based on aggregation.
        /// </summary>
        public long ExpectedValue { get; set; }

        /// <summary>
        /// Actual value found.
        /// </summary>
        public long ActualValue { get; set; }

        /// <summary>
        /// Drift percentage.
        /// </summary>
        public double DriftPercentage { get; set; }

        /// <summary>
        /// Instances involved in the discrepancy.
        /// </summary>
        public List<string> AffectedInstances { get; set; } = new();
    }

    /// <summary>
    /// Type of discrepancy detected.
    /// </summary>
    public enum DiscrepancyType
    {
        /// <summary>
        /// Sum of instance counts doesn't match aggregated total.
        /// </summary>
        CountMismatch,

        /// <summary>
        /// Instance reporting data for wrong time period.
        /// </summary>
        TimeSkew,

        /// <summary>
        /// Instance missing expected data.
        /// </summary>
        MissingData,

        /// <summary>
        /// Duplicate data detected.
        /// </summary>
        DuplicateData
    }

    /// <summary>
    /// Performance metrics for the statistics system.
    /// </summary>
    public class StatisticsPerformanceMetrics
    {
        /// <summary>
        /// Average operation recording latency in milliseconds.
        /// </summary>
        public double AvgRecordingLatencyMs { get; set; }

        /// <summary>
        /// 95th percentile recording latency.
        /// </summary>
        public double P95RecordingLatencyMs { get; set; }

        /// <summary>
        /// 99th percentile recording latency.
        /// </summary>
        public double P99RecordingLatencyMs { get; set; }

        /// <summary>
        /// Average aggregation query latency.
        /// </summary>
        public double AvgAggregationLatencyMs { get; set; }

        /// <summary>
        /// Operations recorded per second.
        /// </summary>
        public double OperationsPerSecond { get; set; }

        /// <summary>
        /// Redis operations per second.
        /// </summary>
        public double RedisOpsPerSecond { get; set; }

        /// <summary>
        /// Current Redis memory usage in bytes.
        /// </summary>
        public long RedisMemoryBytes { get; set; }

        /// <summary>
        /// Number of active instances.
        /// </summary>
        public int ActiveInstances { get; set; }

        /// <summary>
        /// Timestamp of metrics collection.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Per-region performance metrics.
        /// </summary>
        public Dictionary<CacheRegion, RegionPerformanceMetrics> RegionMetrics { get; set; } = new();
    }

    /// <summary>
    /// Performance metrics for a specific cache region.
    /// </summary>
    public class RegionPerformanceMetrics
    {
        /// <summary>
        /// Operations per second for this region.
        /// </summary>
        public double OperationsPerSecond { get; set; }

        /// <summary>
        /// Average latency for this region.
        /// </summary>
        public double AvgLatencyMs { get; set; }

        /// <summary>
        /// Data volume in bytes.
        /// </summary>
        public long DataVolumeBytes { get; set; }
    }

    /// <summary>
    /// Alert thresholds for statistics monitoring.
    /// </summary>
    public class StatisticsAlertThresholds
    {
        /// <summary>
        /// Maximum time an instance can be missing before alerting (default: 1 minute).
        /// </summary>
        public TimeSpan MaxInstanceMissingTime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Maximum aggregation latency before alerting (default: 500ms).
        /// </summary>
        public TimeSpan MaxAggregationLatency { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Maximum allowed drift percentage between instances (default: 5%).
        /// </summary>
        public double MaxDriftPercentage { get; set; } = 5.0;

        /// <summary>
        /// Maximum Redis memory usage in bytes (default: 1GB).
        /// </summary>
        public long MaxRedisMemoryBytes { get; set; } = 1024 * 1024 * 1024; // 1GB

        /// <summary>
        /// Maximum recording latency P99 (default: 10ms).
        /// </summary>
        public double MaxRecordingLatencyP99Ms { get; set; } = 10.0;

        /// <summary>
        /// Minimum active instances before alerting.
        /// </summary>
        public int MinActiveInstances { get; set; } = 1;
    }

    /// <summary>
    /// Monitoring alert for statistics system.
    /// </summary>
    public class StatisticsMonitoringAlert
    {
        /// <summary>
        /// Unique alert identifier.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Alert type.
        /// </summary>
        public StatisticsAlertType Type { get; set; }

        /// <summary>
        /// Alert severity.
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// When the alert was triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; }

        /// <summary>
        /// Current value that triggered the alert.
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Threshold value that was exceeded.
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// Additional context data.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();

        /// <summary>
        /// Whether the alert has been acknowledged.
        /// </summary>
        public bool IsAcknowledged { get; set; }
    }

    /// <summary>
    /// Type of statistics monitoring alert.
    /// </summary>
    public enum StatisticsAlertType
    {
        /// <summary>
        /// Instance not reporting statistics.
        /// </summary>
        InstanceNotReporting,

        /// <summary>
        /// High aggregation latency detected.
        /// </summary>
        HighAggregationLatency,

        /// <summary>
        /// Statistics drift between instances.
        /// </summary>
        StatisticsDrift,

        /// <summary>
        /// High Redis memory usage.
        /// </summary>
        HighRedisMemory,

        /// <summary>
        /// High recording latency.
        /// </summary>
        HighRecordingLatency,

        /// <summary>
        /// Low number of active instances.
        /// </summary>
        LowActiveInstances,

        /// <summary>
        /// Redis connection failure.
        /// </summary>
        RedisConnectionFailure
    }

    /// <summary>
    /// Event arguments for statistics monitoring alerts.
    /// </summary>
    public class StatisticsMonitoringAlertEventArgs : EventArgs
    {
        /// <summary>
        /// The triggered alert.
        /// </summary>
        public StatisticsMonitoringAlert Alert { get; set; } = null!;

        /// <summary>
        /// Whether this is a new alert or an update.
        /// </summary>
        public bool IsNew { get; set; }
    }
}