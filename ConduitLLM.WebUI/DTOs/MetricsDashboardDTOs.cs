using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Real-time metrics snapshot sent via SignalR to dashboard clients.
    /// </summary>
    public class MetricsSnapshot
    {
        /// <summary>
        /// Timestamp when this snapshot was captured.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// HTTP request metrics.
        /// </summary>
        public HttpMetrics Http { get; set; } = new();

        /// <summary>
        /// Infrastructure metrics.
        /// </summary>
        public InfrastructureMetrics Infrastructure { get; set; } = new();

        /// <summary>
        /// Business metrics.
        /// </summary>
        public BusinessMetrics Business { get; set; } = new();

        /// <summary>
        /// Provider health status.
        /// </summary>
        public List<ProviderHealthStatus> ProviderHealth { get; set; } = new();

        /// <summary>
        /// System resource metrics.
        /// </summary>
        public SystemMetrics System { get; set; } = new();
    }

    /// <summary>
    /// HTTP request metrics.
    /// </summary>
    public class HttpMetrics
    {
        /// <summary>
        /// Total requests per second.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Active concurrent requests.
        /// </summary>
        public int ActiveRequests { get; set; }

        /// <summary>
        /// Request rate by endpoint.
        /// </summary>
        public Dictionary<string, double> EndpointRequestRates { get; set; } = new();

        /// <summary>
        /// Response time percentiles in milliseconds.
        /// </summary>
        public ResponseTimePercentiles ResponseTimes { get; set; } = new();

        /// <summary>
        /// Error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Status code distribution.
        /// </summary>
        public Dictionary<int, int> StatusCodeCounts { get; set; } = new();

        /// <summary>
        /// Rate limit hits per minute.
        /// </summary>
        public int RateLimitHitsPerMinute { get; set; }
    }

    /// <summary>
    /// Response time percentiles.
    /// </summary>
    public class ResponseTimePercentiles
    {
        public double P50 { get; set; }
        public double P90 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
    }

    /// <summary>
    /// Infrastructure metrics.
    /// </summary>
    public class InfrastructureMetrics
    {
        /// <summary>
        /// Database metrics.
        /// </summary>
        public DatabaseMetrics Database { get; set; } = new();

        /// <summary>
        /// Redis cache metrics.
        /// </summary>
        public RedisMetrics Redis { get; set; } = new();

        /// <summary>
        /// RabbitMQ metrics.
        /// </summary>
        public RabbitMQMetrics RabbitMQ { get; set; } = new();

        /// <summary>
        /// SignalR metrics.
        /// </summary>
        public SignalRMetrics SignalR { get; set; } = new();
    }

    /// <summary>
    /// Database metrics.
    /// </summary>
    public class DatabaseMetrics
    {
        /// <summary>
        /// Number of active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Number of available connections.
        /// </summary>
        public int AvailableConnections { get; set; }

        /// <summary>
        /// Connection pool utilization percentage.
        /// </summary>
        public double PoolUtilization { get; set; }

        /// <summary>
        /// Average query duration in milliseconds.
        /// </summary>
        public double AverageQueryDuration { get; set; }

        /// <summary>
        /// Database errors per minute.
        /// </summary>
        public int ErrorsPerMinute { get; set; }

        /// <summary>
        /// Health status.
        /// </summary>
        public string HealthStatus { get; set; } = "healthy";
    }

    /// <summary>
    /// Redis cache metrics.
    /// </summary>
    public class RedisMetrics
    {
        /// <summary>
        /// Memory usage in MB.
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Total number of keys.
        /// </summary>
        public long KeyCount { get; set; }

        /// <summary>
        /// Connected clients.
        /// </summary>
        public int ConnectedClients { get; set; }

        /// <summary>
        /// Cache hit rate percentage.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Operations per second.
        /// </summary>
        public double OperationsPerSecond { get; set; }

        /// <summary>
        /// Average operation latency in milliseconds.
        /// </summary>
        public double AverageLatency { get; set; }

        /// <summary>
        /// Connection status.
        /// </summary>
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// RabbitMQ metrics.
    /// </summary>
    public class RabbitMQMetrics
    {
        /// <summary>
        /// Connection status.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Queue depths by queue name.
        /// </summary>
        public Dictionary<string, int> QueueDepths { get; set; } = new();

        /// <summary>
        /// Messages published per minute.
        /// </summary>
        public int MessagesPublishedPerMinute { get; set; }

        /// <summary>
        /// Messages consumed per minute.
        /// </summary>
        public int MessagesConsumedPerMinute { get; set; }

        /// <summary>
        /// Consumer count by queue.
        /// </summary>
        public Dictionary<string, int> ConsumerCounts { get; set; } = new();
    }

    /// <summary>
    /// SignalR metrics.
    /// </summary>
    public class SignalRMetrics
    {
        /// <summary>
        /// Active WebSocket connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Messages sent per minute.
        /// </summary>
        public int MessagesSentPerMinute { get; set; }

        /// <summary>
        /// Messages received per minute.
        /// </summary>
        public int MessagesReceivedPerMinute { get; set; }

        /// <summary>
        /// Average message processing time in milliseconds.
        /// </summary>
        public double AverageMessageProcessingTime { get; set; }

        /// <summary>
        /// Hub method invocations per minute.
        /// </summary>
        public int HubInvocationsPerMinute { get; set; }

        /// <summary>
        /// Reconnection rate per minute.
        /// </summary>
        public int ReconnectionsPerMinute { get; set; }
    }

    /// <summary>
    /// Business metrics.
    /// </summary>
    public class BusinessMetrics
    {
        /// <summary>
        /// Active virtual keys count.
        /// </summary>
        public int ActiveVirtualKeys { get; set; }

        /// <summary>
        /// Total requests per minute.
        /// </summary>
        public int TotalRequestsPerMinute { get; set; }

        /// <summary>
        /// Cost metrics.
        /// </summary>
        public CostMetrics Costs { get; set; } = new();

        /// <summary>
        /// Model usage statistics.
        /// </summary>
        public List<ModelUsageStats> ModelUsage { get; set; } = new();

        /// <summary>
        /// Virtual key statistics.
        /// </summary>
        public List<VirtualKeyStats> TopVirtualKeys { get; set; } = new();
    }

    /// <summary>
    /// Cost metrics.
    /// </summary>
    public class CostMetrics
    {
        /// <summary>
        /// Total cost rate in dollars per minute.
        /// </summary>
        public decimal TotalCostPerMinute { get; set; }

        /// <summary>
        /// Cost breakdown by provider.
        /// </summary>
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();

        /// <summary>
        /// Average cost per request.
        /// </summary>
        public decimal AverageCostPerRequest { get; set; }
    }

    /// <summary>
    /// Model usage statistics.
    /// </summary>
    public class ModelUsageStats
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Requests per minute.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Total tokens processed per minute.
        /// </summary>
        public long TokensPerMinute { get; set; }

        /// <summary>
        /// Average response time in milliseconds.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }
    }

    /// <summary>
    /// Virtual key statistics.
    /// </summary>
    public class VirtualKeyStats
    {
        /// <summary>
        /// Virtual key ID.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Requests per minute.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Total spend.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Budget utilization percentage.
        /// </summary>
        public double BudgetUtilization { get; set; }

        /// <summary>
        /// Is over budget.
        /// </summary>
        public bool IsOverBudget { get; set; }
    }

    /// <summary>
    /// Provider health status.
    /// </summary>
    public class ProviderHealthStatus
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Health status: healthy, degraded, or unhealthy.
        /// </summary>
        public string Status { get; set; } = "healthy";

        /// <summary>
        /// Last successful request timestamp.
        /// </summary>
        public DateTime? LastSuccessfulRequest { get; set; }

        /// <summary>
        /// Error rate percentage.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Average latency in milliseconds.
        /// </summary>
        public double AverageLatency { get; set; }

        /// <summary>
        /// Number of available models.
        /// </summary>
        public int AvailableModels { get; set; }

        /// <summary>
        /// Is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// System resource metrics.
    /// </summary>
    public class SystemMetrics
    {
        /// <summary>
        /// CPU usage percentage.
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Memory usage in MB.
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Thread count.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Handle count.
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// GC memory in MB.
        /// </summary>
        public double GcMemoryMB { get; set; }

        /// <summary>
        /// Process uptime.
        /// </summary>
        public TimeSpan Uptime { get; set; }
    }

    /// <summary>
    /// Historical metrics data point.
    /// </summary>
    public class MetricsDataPoint
    {
        /// <summary>
        /// Timestamp of the data point.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Metric value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Optional label for categorization.
        /// </summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// Historical metrics series.
    /// </summary>
    public class MetricsSeries
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Series label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Data points in the series.
        /// </summary>
        public List<MetricsDataPoint> DataPoints { get; set; } = new();
    }

    /// <summary>
    /// Request for historical metrics.
    /// </summary>
    public class HistoricalMetricsRequest
    {
        /// <summary>
        /// Start time for the query.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time for the query.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Metric names to retrieve.
        /// </summary>
        public List<string> MetricNames { get; set; } = new();

        /// <summary>
        /// Time interval for aggregation (e.g., "1m", "5m", "1h").
        /// </summary>
        public string Interval { get; set; } = "1m";

        /// <summary>
        /// Optional filters by label.
        /// </summary>
        public Dictionary<string, string>? Filters { get; set; }
    }

    /// <summary>
    /// Response containing historical metrics.
    /// </summary>
    public class HistoricalMetricsResponse
    {
        /// <summary>
        /// Query time range.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Query end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Aggregation interval.
        /// </summary>
        public string Interval { get; set; } = string.Empty;

        /// <summary>
        /// Metrics series data.
        /// </summary>
        public List<MetricsSeries> Series { get; set; } = new();
    }

    /// <summary>
    /// Metric alert information.
    /// </summary>
    public class MetricAlert
    {
        /// <summary>
        /// Alert ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity (info, warning, critical).
        /// </summary>
        public string Severity { get; set; } = "info";

        /// <summary>
        /// Metric that triggered the alert.
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Current metric value.
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Threshold value.
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// When the alert was triggered.
        /// </summary>
        public DateTime TriggeredAt { get; set; }

        /// <summary>
        /// Is the alert currently active.
        /// </summary>
        public bool IsActive { get; set; }
    }
}