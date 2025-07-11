using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Comprehensive snapshot of system metrics at a point in time.
/// </summary>
public class MetricsSnapshot
{
    /// <summary>
    /// Gets or sets the timestamp when these metrics were captured.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets HTTP request and response metrics.
    /// </summary>
    public HttpMetrics Http { get; set; } = new();

    /// <summary>
    /// Gets or sets infrastructure component metrics.
    /// </summary>
    public InfrastructureMetrics Infrastructure { get; set; } = new();

    /// <summary>
    /// Gets or sets business logic metrics.
    /// </summary>
    public BusinessMetrics Business { get; set; } = new();

    /// <summary>
    /// Gets or sets provider health status information.
    /// </summary>
    public List<ProviderHealthStatus> ProviderHealth { get; set; } = new();

    /// <summary>
    /// Gets or sets system resource metrics.
    /// </summary>
    public SystemMetrics System { get; set; } = new();
}

/// <summary>
/// HTTP request and response metrics.
/// </summary>
public class HttpMetrics
{
    /// <summary>
    /// Gets or sets the number of requests per second.
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the current number of active requests.
    /// </summary>
    public int ActiveRequests { get; set; }

    /// <summary>
    /// Gets or sets request rates per endpoint.
    /// </summary>
    public Dictionary<string, double> EndpointRequestRates { get; set; } = new();

    /// <summary>
    /// Gets or sets response time percentiles.
    /// </summary>
    public ResponseTimeMetrics ResponseTimes { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall error rate as a percentage.
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets counts of responses by HTTP status code.
    /// </summary>
    public Dictionary<string, int> StatusCodeCounts { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of rate limit hits per minute.
    /// </summary>
    public double RateLimitHitsPerMinute { get; set; }
}

/// <summary>
/// Response time metrics with percentiles.
/// </summary>
public class ResponseTimeMetrics
{
    /// <summary>
    /// Gets or sets the 50th percentile (median) response time in milliseconds.
    /// </summary>
    public double P50 { get; set; }

    /// <summary>
    /// Gets or sets the 90th percentile response time in milliseconds.
    /// </summary>
    public double P90 { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile response time in milliseconds.
    /// </summary>
    public double P95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile response time in milliseconds.
    /// </summary>
    public double P99 { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double Average { get; set; }
}

/// <summary>
/// Infrastructure component metrics.
/// </summary>
public class InfrastructureMetrics
{
    /// <summary>
    /// Gets or sets database metrics.
    /// </summary>
    public DatabaseMetrics Database { get; set; } = new();

    /// <summary>
    /// Gets or sets Redis cache metrics.
    /// </summary>
    public RedisMetrics Redis { get; set; } = new();

    /// <summary>
    /// Gets or sets RabbitMQ message queue metrics.
    /// </summary>
    public RabbitMQMetrics? RabbitMQ { get; set; }

    /// <summary>
    /// Gets or sets SignalR real-time communication metrics.
    /// </summary>
    public SignalRMetrics SignalR { get; set; } = new();
}

/// <summary>
/// Database connection and performance metrics.
/// </summary>
public class DatabaseMetrics
{
    /// <summary>
    /// Gets or sets the number of active database connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed database connections.
    /// </summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// Gets or sets the connection pool utilization percentage.
    /// </summary>
    public double PoolUtilization => MaxConnections > 0 ? (double)ActiveConnections / MaxConnections * 100 : 0;

    /// <summary>
    /// Gets or sets the average query duration in milliseconds.
    /// </summary>
    public double AverageQueryDuration { get; set; }

    /// <summary>
    /// Gets or sets the number of queries per second.
    /// </summary>
    public double QueriesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the number of failed queries per minute.
    /// </summary>
    public double FailedQueriesPerMinute { get; set; }
}

/// <summary>
/// Redis cache metrics.
/// </summary>
public class RedisMetrics
{
    /// <summary>
    /// Gets or sets the Redis memory usage in megabytes.
    /// </summary>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the total number of keys in Redis.
    /// </summary>
    public long KeyCount { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate as a percentage.
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Gets or sets the average Redis operation latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Gets or sets the number of operations per second.
    /// </summary>
    public double OperationsPerSecond { get; set; }
}

/// <summary>
/// RabbitMQ message queue metrics.
/// </summary>
public class RabbitMQMetrics
{
    /// <summary>
    /// Gets or sets queue depths by queue name.
    /// </summary>
    public Dictionary<string, int> QueueDepths { get; set; } = new();

    /// <summary>
    /// Gets or sets message rates by queue name.
    /// </summary>
    public Dictionary<string, double> MessageRates { get; set; } = new();

    /// <summary>
    /// Gets or sets consumer counts by queue name.
    /// </summary>
    public Dictionary<string, int> ConsumerCounts { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of unacknowledged messages.
    /// </summary>
    public int UnacknowledgedMessages { get; set; }

    /// <summary>
    /// Gets or sets the connection count.
    /// </summary>
    public int ConnectionCount { get; set; }
}

/// <summary>
/// SignalR real-time communication metrics.
/// </summary>
public class SignalRMetrics
{
    /// <summary>
    /// Gets or sets the number of active SignalR connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the message rate per second.
    /// </summary>
    public double MessagesPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the average message processing time in milliseconds.
    /// </summary>
    public double AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets connection counts by hub type.
    /// </summary>
    public Dictionary<string, int> ConnectionsByHub { get; set; } = new();
}

/// <summary>
/// Business logic and application metrics.
/// </summary>
public class BusinessMetrics
{
    /// <summary>
    /// Gets or sets the number of active virtual keys.
    /// </summary>
    public int ActiveVirtualKeys { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests per minute.
    /// </summary>
    public double TotalRequestsPerMinute { get; set; }

    /// <summary>
    /// Gets or sets cost-related metrics.
    /// </summary>
    public CostMetrics Cost { get; set; } = new();

    /// <summary>
    /// Gets or sets model usage statistics.
    /// </summary>
    public List<ModelUsageStats> ModelUsage { get; set; } = new();

    /// <summary>
    /// Gets or sets virtual key statistics.
    /// </summary>
    public List<VirtualKeyStats> VirtualKeyStats { get; set; } = new();
}

/// <summary>
/// Cost-related metrics.
/// </summary>
public class CostMetrics
{
    /// <summary>
    /// Gets or sets the cost per minute in USD.
    /// </summary>
    public decimal CostPerMinute { get; set; }

    /// <summary>
    /// Gets or sets cost breakdown by provider.
    /// </summary>
    public Dictionary<string, decimal> CostByProvider { get; set; } = new();

    /// <summary>
    /// Gets or sets the average cost per request in USD.
    /// </summary>
    public decimal AverageCostPerRequest { get; set; }

    /// <summary>
    /// Gets or sets the total spend in the current billing period.
    /// </summary>
    public decimal TotalSpendCurrentPeriod { get; set; }
}

/// <summary>
/// Model usage statistics.
/// </summary>
public class ModelUsageStats
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    [Required]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of requests per minute.
    /// </summary>
    public double RequestsPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the total tokens processed per minute.
    /// </summary>
    public long TokensPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage.
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the cost per minute for this model.
    /// </summary>
    public decimal CostPerMinute { get; set; }
}

/// <summary>
/// Virtual key performance statistics.
/// </summary>
public class VirtualKeyStats
{
    /// <summary>
    /// Gets or sets the virtual key identifier.
    /// </summary>
    [Required]
    public string VirtualKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the virtual key name.
    /// </summary>
    public string? VirtualKeyName { get; set; }

    /// <summary>
    /// Gets or sets the current spend amount.
    /// </summary>
    public decimal CurrentSpend { get; set; }

    /// <summary>
    /// Gets or sets the budget utilization percentage.
    /// </summary>
    public double BudgetUtilization { get; set; }

    /// <summary>
    /// Gets or sets the request rate per minute.
    /// </summary>
    public double RequestsPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the average cost per request.
    /// </summary>
    public decimal AverageCostPerRequest { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage.
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// System resource metrics.
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Gets or sets the CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in megabytes.
    /// </summary>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the current thread count.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the garbage collector memory usage in megabytes.
    /// </summary>
    public double GcMemoryMB { get; set; }

    /// <summary>
    /// Gets or sets the system uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the process start time.
    /// </summary>
    public DateTime ProcessStartTime { get; set; }
}

/// <summary>
/// Provider health status information.
/// </summary>
public class ProviderHealthStatus
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current health status.
    /// </summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the last successful request.
    /// </summary>
    public DateTime? LastSuccessfulRequest { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage.
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Gets or sets the number of available models for this provider.
    /// </summary>
    public int AvailableModels { get; set; }

    /// <summary>
    /// Gets or sets whether the provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets whether the provider is currently healthy.
    /// </summary>
    [JsonIgnore]
    public bool IsHealthy => Status.Equals("healthy", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Request for historical metrics data.
/// </summary>
public class HistoricalMetricsRequest
{
    /// <summary>
    /// Gets or sets the start time for the metrics query.
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for the metrics query.
    /// </summary>
    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the list of metric names to retrieve.
    /// </summary>
    public List<string> MetricNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregation interval (e.g., "1m", "5m", "1h").
    /// </summary>
    public string Interval { get; set; } = "1m";

    /// <summary>
    /// Gets or sets additional filters for the query.
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }
}

/// <summary>
/// Response containing historical metrics data.
/// </summary>
public class HistoricalMetricsResponse
{
    /// <summary>
    /// Gets or sets the start time of the data range.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the data range.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the aggregation interval used.
    /// </summary>
    public string Interval { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time series data.
    /// </summary>
    public List<MetricsSeries> Series { get; set; } = new();
}

/// <summary>
/// Time series data for a specific metric.
/// </summary>
public class MetricsSeries
{
    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    [Required]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time series data points.
    /// </summary>
    public List<MetricsDataPoint> DataPoints { get; set; } = new();

    /// <summary>
    /// Gets or sets metadata about the metric.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// A single data point in a time series.
/// </summary>
public class MetricsDataPoint
{
    /// <summary>
    /// Gets or sets the timestamp of this data point.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the metric value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets additional labels for this data point.
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }
}

/// <summary>
/// Metric alert information.
/// </summary>
public class MetricAlert
{
    /// <summary>
    /// Gets or sets the unique alert identifier.
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alert severity level.
    /// </summary>
    [Required]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the metric that triggered the alert.
    /// </summary>
    [Required]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alert message.
    /// </summary>
    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current value of the metric.
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// Gets or sets the threshold that was exceeded.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets when the alert was triggered.
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// Gets or sets whether the alert is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets additional context about the alert.
    /// </summary>
    public Dictionary<string, string>? Context { get; set; }
}