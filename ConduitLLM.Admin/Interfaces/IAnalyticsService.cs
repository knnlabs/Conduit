using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Unified service for analytics, combining logs, costs, and usage metrics
/// </summary>
public interface IAnalyticsService
{
    // Request Logs
    
    /// <summary>
    /// Gets paginated request logs with optional filters
    /// </summary>
    Task<PagedResult<LogRequestDto>> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? model = null,
        int? virtualKeyId = null,
        int? status = null);

    /// <summary>
    /// Gets a single log entry by ID
    /// </summary>
    Task<LogRequestDto?> GetLogByIdAsync(int id);

    /// <summary>
    /// Gets distinct model names from request logs
    /// </summary>
    Task<IEnumerable<string>> GetDistinctModelsAsync();

    // Cost Analytics
    
    /// <summary>
    /// Gets cost dashboard summary data
    /// </summary>
    Task<CostDashboardDto> GetCostSummaryAsync(
        string timeframe = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Gets cost trend data over time
    /// </summary>
    Task<CostTrendDto> GetCostTrendsAsync(
        string period = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Gets costs grouped by model
    /// </summary>
    Task<ModelCostBreakdownDto> GetModelCostsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int topN = 10);

    /// <summary>
    /// Gets costs grouped by virtual key
    /// </summary>
    Task<VirtualKeyCostBreakdownDto> GetVirtualKeyCostsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int topN = 10);

    // Combined Analytics
    
    /// <summary>
    /// Gets comprehensive analytics summary combining logs and costs
    /// </summary>
    Task<AnalyticsSummaryDto> GetAnalyticsSummaryAsync(
        string timeframe = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Gets usage statistics for a specific virtual key
    /// </summary>
    Task<UsageStatisticsDto> GetVirtualKeyUsageAsync(
        int virtualKeyId,
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Exports analytics data in various formats
    /// </summary>
    Task<byte[]> ExportAnalyticsAsync(
        string format = "csv",
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? model = null,
        int? virtualKeyId = null);
}

/// <summary>
/// Combined analytics summary DTO
/// </summary>
public class AnalyticsSummaryDto
{
    /// <summary>
    /// Total number of requests in the period
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Total cost in the period
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Total input tokens processed
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// Total output tokens generated
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Success rate as percentage (0-100)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Number of unique virtual keys used
    /// </summary>
    public int UniqueVirtualKeys { get; set; }

    /// <summary>
    /// Number of unique models used
    /// </summary>
    public int UniqueModels { get; set; }

    /// <summary>
    /// Top models by usage
    /// </summary>
    public List<ModelUsageSummary> TopModels { get; set; } = new();

    /// <summary>
    /// Top virtual keys by cost
    /// </summary>
    public List<VirtualKeyUsageSummary> TopVirtualKeys { get; set; } = new();

    /// <summary>
    /// Daily statistics for the period
    /// </summary>
    public List<DailyStatistics> DailyStats { get; set; } = new();

    /// <summary>
    /// Comparison with previous period
    /// </summary>
    public PeriodComparison? Comparison { get; set; }
}

/// <summary>
/// Model usage summary
/// </summary>
public class ModelUsageSummary
{
    /// <summary>
    /// Name of the model
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of requests for this model
    /// </summary>
    public int RequestCount { get; set; }
    
    /// <summary>
    /// Total cost incurred by this model
    /// </summary>
    public decimal TotalCost { get; set; }
    
    /// <summary>
    /// Total input tokens processed
    /// </summary>
    public long InputTokens { get; set; }
    
    /// <summary>
    /// Total output tokens generated
    /// </summary>
    public long OutputTokens { get; set; }
    
    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; set; }
    
    /// <summary>
    /// Error rate as percentage (0-100)
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// Virtual key usage summary
/// </summary>
public class VirtualKeyUsageSummary
{
    /// <summary>
    /// Virtual key identifier
    /// </summary>
    public int VirtualKeyId { get; set; }
    
    /// <summary>
    /// Name of the virtual key
    /// </summary>
    public string KeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of requests
    /// </summary>
    public int RequestCount { get; set; }
    
    /// <summary>
    /// Total cost incurred
    /// </summary>
    public decimal TotalCost { get; set; }
    
    /// <summary>
    /// Last time this key was used
    /// </summary>
    public DateTime? LastUsed { get; set; }
    
    /// <summary>
    /// List of model names used with this key
    /// </summary>
    public List<string> ModelsUsed { get; set; } = new();
}

/// <summary>
/// Daily statistics
/// </summary>
public class DailyStatistics
{
    /// <summary>
    /// Date for these statistics
    /// </summary>
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Number of requests on this date
    /// </summary>
    public int RequestCount { get; set; }
    
    /// <summary>
    /// Total cost for this date
    /// </summary>
    public decimal Cost { get; set; }
    
    /// <summary>
    /// Total input tokens for this date
    /// </summary>
    public long InputTokens { get; set; }
    
    /// <summary>
    /// Total output tokens for this date
    /// </summary>
    public long OutputTokens { get; set; }
    
    /// <summary>
    /// Average response time for this date
    /// </summary>
    public double AverageResponseTime { get; set; }
    
    /// <summary>
    /// Number of errors on this date
    /// </summary>
    public int ErrorCount { get; set; }
}

/// <summary>
/// Period comparison for trend analysis
/// </summary>
public class PeriodComparison
{
    /// <summary>
    /// Absolute change in cost compared to previous period
    /// </summary>
    public decimal CostChange { get; set; }
    
    /// <summary>
    /// Percentage change in cost compared to previous period
    /// </summary>
    public decimal CostChangePercentage { get; set; }
    
    /// <summary>
    /// Absolute change in request count compared to previous period
    /// </summary>
    public int RequestChange { get; set; }
    
    /// <summary>
    /// Percentage change in request count compared to previous period
    /// </summary>
    public decimal RequestChangePercentage { get; set; }
    
    /// <summary>
    /// Change in average response time compared to previous period
    /// </summary>
    public double ResponseTimeChange { get; set; }
    
    /// <summary>
    /// Change in error rate compared to previous period
    /// </summary>
    public double ErrorRateChange { get; set; }
}