namespace ConduitLLM.Configuration.DTOs;

/// <summary>
/// Aggregated cost data grouped by date, computed at the database level.
/// Used to replace in-memory GroupBy operations on full request log datasets.
/// </summary>
public class DateCostAggregation
{
    /// <summary>Date for this aggregation bucket</summary>
    public DateTime Date { get; set; }

    /// <summary>Sum of all costs for this date</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Number of requests on this date</summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// Aggregated request log data grouped by model, computed at the database level.
/// </summary>
public class ModelAggregation
{
    /// <summary>Model name</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Sum of all costs for this model</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Number of requests for this model</summary>
    public int RequestCount { get; set; }

    /// <summary>Sum of input tokens</summary>
    public long InputTokens { get; set; }

    /// <summary>Sum of output tokens</summary>
    public long OutputTokens { get; set; }
}

/// <summary>
/// Aggregated request log data grouped by virtual key, computed at the database level.
/// </summary>
public class VirtualKeyAggregation
{
    /// <summary>Virtual key identifier</summary>
    public int VirtualKeyId { get; set; }

    /// <summary>Sum of all costs for this key</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Number of requests for this key</summary>
    public int RequestCount { get; set; }

    /// <summary>Most recent request timestamp</summary>
    public DateTime LastUsed { get; set; }

    /// <summary>Number of distinct models used with this key</summary>
    public int UniqueModels { get; set; }
}

/// <summary>
/// Summary statistics for request logs within a date range, computed at the database level
/// as a single aggregate row (no grouping).
/// </summary>
public class RequestLogSummary
{
    /// <summary>Total number of requests</summary>
    public int TotalRequests { get; set; }

    /// <summary>Sum of all costs</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Sum of input tokens</summary>
    public long TotalInputTokens { get; set; }

    /// <summary>Sum of output tokens</summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>Average response time in milliseconds</summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>Number of requests with status code in 200-299 range</summary>
    public int SuccessCount { get; set; }

    /// <summary>Number of requests with status code >= 400</summary>
    public int ErrorCount { get; set; }
}

/// <summary>
/// Aggregated daily statistics for request logs, computed at the database level.
/// Can be further aggregated to weekly/monthly in C# with minimal overhead.
/// </summary>
public class DailyStatisticsAggregation
{
    /// <summary>Date for these statistics</summary>
    public DateTime Date { get; set; }

    /// <summary>Number of requests on this date</summary>
    public int RequestCount { get; set; }

    /// <summary>Sum of all costs for this date</summary>
    public decimal Cost { get; set; }

    /// <summary>Sum of input tokens for this date</summary>
    public long InputTokens { get; set; }

    /// <summary>Sum of output tokens for this date</summary>
    public long OutputTokens { get; set; }

    /// <summary>Average response time in milliseconds for this date</summary>
    public double AverageResponseTime { get; set; }

    /// <summary>Number of requests with status code >= 400 on this date</summary>
    public int ErrorCount { get; set; }
}
