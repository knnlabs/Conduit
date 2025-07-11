namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a cost summary for analytics.
/// </summary>
public class CostSummaryDto
{
    /// <summary>
    /// Gets or sets the total cost for the period.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the total number of input tokens.
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// Gets or sets the total number of output tokens.
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the currency used for costs.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets the period for this cost summary.
    /// </summary>
    public DateRange Period { get; set; } = new();

    /// <summary>
    /// Gets or sets the cost breakdown by model.
    /// </summary>
    public IEnumerable<CostByModel> CostByModel { get; set; } = new List<CostByModel>();

    /// <summary>
    /// Gets or sets the cost breakdown by virtual key.
    /// </summary>
    public IEnumerable<CostByKey> CostByKey { get; set; } = new List<CostByKey>();

    /// <summary>
    /// Gets or sets the cost breakdown by provider.
    /// </summary>
    public IEnumerable<CostByProvider> CostByProvider { get; set; } = new List<CostByProvider>();
}

/// <summary>
/// Represents cost information by model.
/// </summary>
public class CostByModel
{
    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total cost for this model.
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Gets or sets the input tokens for this model.
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the output tokens for this model.
    /// </summary>
    public long OutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the request count for this model.
    /// </summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// Represents cost information by virtual key.
/// </summary>
public class CostByKey
{
    /// <summary>
    /// Gets or sets the virtual key ID.
    /// </summary>
    public int KeyId { get; set; }

    /// <summary>
    /// Gets or sets the virtual key name.
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total cost for this key.
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Gets or sets the request count for this key.
    /// </summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// Represents cost information by provider.
/// </summary>
public class CostByProvider
{
    /// <summary>
    /// Gets or sets the provider identifier.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total cost for this provider.
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Gets or sets the request count for this provider.
    /// </summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// Represents cost trends over time periods.
/// </summary>
public class CostByPeriodDto
{
    /// <summary>
    /// Gets or sets the cost data for each period.
    /// </summary>
    public IEnumerable<PeriodCost> Periods { get; set; } = new List<PeriodCost>();

    /// <summary>
    /// Gets or sets the total cost across all periods.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the average cost per period.
    /// </summary>
    public decimal AverageCostPerPeriod { get; set; }

    /// <summary>
    /// Gets or sets the cost trend.
    /// </summary>
    public CostTrend Trend { get; set; }

    /// <summary>
    /// Gets or sets the trend percentage change.
    /// </summary>
    public double TrendPercentage { get; set; }
}

/// <summary>
/// Represents cost data for a specific period.
/// </summary>
public class PeriodCost
{
    /// <summary>
    /// Gets or sets the period identifier.
    /// </summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start date of the period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the period.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Gets or sets the total cost for this period.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the input tokens for this period.
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the output tokens for this period.
    /// </summary>
    public long OutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the request count for this period.
    /// </summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// Represents cost trend directions.
/// </summary>
public enum CostTrend
{
    /// <summary>
    /// Cost is increasing.
    /// </summary>
    Increasing,

    /// <summary>
    /// Cost is decreasing.
    /// </summary>
    Decreasing,

    /// <summary>
    /// Cost is stable.
    /// </summary>
    Stable
}

/// <summary>
/// Represents a request log entry.
/// </summary>
public class RequestLogDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the request.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the request.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the virtual key ID used for the request.
    /// </summary>
    public int? VirtualKeyId { get; set; }

    /// <summary>
    /// Gets or sets the virtual key name used for the request.
    /// </summary>
    public string? VirtualKeyName { get; set; }

    /// <summary>
    /// Gets or sets the model used for the request.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider that handled the request.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of input tokens.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of output tokens.
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the cost of the request.
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Gets or sets the currency used for the cost.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets the duration of the request in milliseconds.
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Gets or sets the status of the request.
    /// </summary>
    public RequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent of the client.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the request headers.
    /// </summary>
    public Dictionary<string, string>? RequestHeaders { get; set; }

    /// <summary>
    /// Gets or sets the response headers.
    /// </summary>
    public Dictionary<string, string>? ResponseHeaders { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the request.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents request status values.
/// </summary>
public enum RequestStatus
{
    /// <summary>
    /// Request completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Request failed with an error.
    /// </summary>
    Error,

    /// <summary>
    /// Request timed out.
    /// </summary>
    Timeout
}

/// <summary>
/// Represents filter options for request logs.
/// </summary>
public class RequestLogFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the start date filter.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date filter.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the virtual key ID filter.
    /// </summary>
    public int? VirtualKeyId { get; set; }

    /// <summary>
    /// Gets or sets the model filter.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the provider filter.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public RequestStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the minimum cost filter.
    /// </summary>
    public decimal? MinCost { get; set; }

    /// <summary>
    /// Gets or sets the maximum cost filter.
    /// </summary>
    public decimal? MaxCost { get; set; }

    /// <summary>
    /// Gets or sets the minimum duration filter in milliseconds.
    /// </summary>
    public int? MinDuration { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration filter in milliseconds.
    /// </summary>
    public int? MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the IP address filter.
    /// </summary>
    public string? IpAddress { get; set; }
}

/// <summary>
/// Represents usage metrics for analytics.
/// </summary>
public class UsageMetricsDto
{
    /// <summary>
    /// Gets or sets the period for these metrics.
    /// </summary>
    public DateRange Period { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of requests.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of successful requests.
    /// </summary>
    public int SuccessfulRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of failed requests.
    /// </summary>
    public int FailedRequests { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile latency in milliseconds.
    /// </summary>
    public double P95Latency { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile latency in milliseconds.
    /// </summary>
    public double P99Latency { get; set; }

    /// <summary>
    /// Gets or sets the average requests per minute.
    /// </summary>
    public double RequestsPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the peak requests per minute.
    /// </summary>
    public double PeakRequestsPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the number of unique keys used.
    /// </summary>
    public int UniqueKeys { get; set; }

    /// <summary>
    /// Gets or sets the number of unique models used.
    /// </summary>
    public int UniqueModels { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage.
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// Represents usage analytics for a specific model.
/// </summary>
public class ModelUsageDto
{
    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of requests for this model.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the total tokens used by this model.
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the total cost for this model.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the average tokens per request.
    /// </summary>
    public double AverageTokensPerRequest { get; set; }

    /// <summary>
    /// Gets or sets the average cost per request.
    /// </summary>
    public decimal AverageCostPerRequest { get; set; }

    /// <summary>
    /// Gets or sets the success rate as a percentage.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Gets or sets the most popular keys using this model.
    /// </summary>
    public IEnumerable<PopularKey> PopularKeys { get; set; } = new List<PopularKey>();
}

/// <summary>
/// Represents a popular virtual key for model usage.
/// </summary>
public class PopularKey
{
    /// <summary>
    /// Gets or sets the virtual key ID.
    /// </summary>
    public int KeyId { get; set; }

    /// <summary>
    /// Gets or sets the virtual key name.
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request count for this key.
    /// </summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// Represents usage analytics for a specific virtual key.
/// </summary>
public class KeyUsageDto
{
    /// <summary>
    /// Gets or sets the virtual key ID.
    /// </summary>
    public int KeyId { get; set; }

    /// <summary>
    /// Gets or sets the virtual key name.
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of requests for this key.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the total cost for this key.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the percentage of budget used.
    /// </summary>
    public decimal BudgetUsed { get; set; }

    /// <summary>
    /// Gets or sets the remaining budget amount.
    /// </summary>
    public decimal BudgetRemaining { get; set; }

    /// <summary>
    /// Gets or sets the average cost per request.
    /// </summary>
    public decimal AverageCostPerRequest { get; set; }

    /// <summary>
    /// Gets or sets the average requests per day.
    /// </summary>
    public double RequestsPerDay { get; set; }

    /// <summary>
    /// Gets or sets the most popular models used by this key.
    /// </summary>
    public IEnumerable<PopularModel> PopularModels { get; set; } = new List<PopularModel>();

    /// <summary>
    /// Gets or sets when the key was last used.
    /// </summary>
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// Represents a popular model for key usage.
/// </summary>
public class PopularModel
{
    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request count for this model.
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the total cost for this model.
    /// </summary>
    public decimal TotalCost { get; set; }
}

/// <summary>
/// Represents filter options for analytics queries.
/// </summary>
public class AnalyticsFilters
{
    /// <summary>
    /// Gets or sets the start date for the analytics period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for the analytics period.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Gets or sets the virtual key IDs to include in analytics.
    /// </summary>
    public IEnumerable<int>? VirtualKeyIds { get; set; }

    /// <summary>
    /// Gets or sets the models to include in analytics.
    /// </summary>
    public IEnumerable<string>? Models { get; set; }

    /// <summary>
    /// Gets or sets the providers to include in analytics.
    /// </summary>
    public IEnumerable<string>? Providers { get; set; }

    /// <summary>
    /// Gets or sets how to group the analytics data.
    /// </summary>
    public AnalyticsGroupBy? GroupBy { get; set; }

    /// <summary>
    /// Gets or sets whether to include metadata in the results.
    /// </summary>
    public bool? IncludeMetadata { get; set; }
}

/// <summary>
/// Represents grouping options for analytics.
/// </summary>
public enum AnalyticsGroupBy
{
    /// <summary>
    /// Group by hour.
    /// </summary>
    Hour,

    /// <summary>
    /// Group by day.
    /// </summary>
    Day,

    /// <summary>
    /// Group by week.
    /// </summary>
    Week,

    /// <summary>
    /// Group by month.
    /// </summary>
    Month
}

/// <summary>
/// Represents a cost forecast.
/// </summary>
public class CostForecastDto
{
    /// <summary>
    /// Gets or sets the period for which the forecast is made.
    /// </summary>
    public DateRange ForecastPeriod { get; set; } = new();

    /// <summary>
    /// Gets or sets the predicted cost for the forecast period.
    /// </summary>
    public decimal PredictedCost { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the forecast (0-1).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the historical period the forecast is based on.
    /// </summary>
    public DateRange BasedOnPeriod { get; set; } = new();

    /// <summary>
    /// Gets or sets the factors influencing the forecast.
    /// </summary>
    public IEnumerable<ForecastFactor> Factors { get; set; } = new List<ForecastFactor>();

    /// <summary>
    /// Gets or sets recommendations to optimize costs.
    /// </summary>
    public IEnumerable<string> Recommendations { get; set; } = new List<string>();
}

/// <summary>
/// Represents a factor influencing cost forecasts.
/// </summary>
public class ForecastFactor
{
    /// <summary>
    /// Gets or sets the name of the factor.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the impact value of the factor.
    /// </summary>
    public double Impact { get; set; }

    /// <summary>
    /// Gets or sets the description of the factor.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents an anomaly detected in the system.
/// </summary>
public class AnomalyDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the anomaly.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the anomaly was detected.
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// Gets or sets the type of anomaly.
    /// </summary>
    public AnomalyType Type { get; set; }

    /// <summary>
    /// Gets or sets the severity of the anomaly.
    /// </summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the description of the anomaly.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resources affected by the anomaly.
    /// </summary>
    public IEnumerable<AffectedResource> AffectedResources { get; set; } = new List<AffectedResource>();

    /// <summary>
    /// Gets or sets the metrics related to the anomaly.
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the anomaly has been resolved.
    /// </summary>
    public bool Resolved { get; set; }
}

/// <summary>
/// Represents types of anomalies.
/// </summary>
public enum AnomalyType
{
    /// <summary>
    /// Sudden increase in costs.
    /// </summary>
    CostSpike,

    /// <summary>
    /// Sudden increase in usage.
    /// </summary>
    UsageSpike,

    /// <summary>
    /// High error rate.
    /// </summary>
    ErrorRate,

    /// <summary>
    /// High latency.
    /// </summary>
    Latency
}

/// <summary>
/// Represents severity levels for anomalies.
/// </summary>
public enum AnomalySeverity
{
    /// <summary>
    /// Low severity anomaly.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity anomaly.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity anomaly.
    /// </summary>
    High
}

/// <summary>
/// Represents a resource affected by an anomaly.
/// </summary>
public class AffectedResource
{
    /// <summary>
    /// Gets or sets the type of resource.
    /// </summary>
    public AffectedResourceType Type { get; set; }

    /// <summary>
    /// Gets or sets the resource identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resource name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Represents types of resources that can be affected by anomalies.
/// </summary>
public enum AffectedResourceType
{
    /// <summary>
    /// Virtual key resource.
    /// </summary>
    Key,

    /// <summary>
    /// Model resource.
    /// </summary>
    Model,

    /// <summary>
    /// Provider resource.
    /// </summary>
    Provider
}