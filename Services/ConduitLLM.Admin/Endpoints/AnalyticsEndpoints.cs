using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Admin.DTOs;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>
/// Unified controller for analytics, logs, and cost data
/// </summary>
public class AnalyticsEndpoints
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IAnalyticsMetrics? _analyticsMetrics;
    private readonly ILogger<AnalyticsEndpoints> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes the Analytics endpoint handler.
    /// </summary>
    /// <param name="analyticsService">The analytics service</param>
    /// <param name="logger">The logger</param>
    /// <param name="httpContextAccessor">Accessor for the current request context</param>
    /// <param name="analyticsMetrics">The analytics metrics service (optional)</param>
    public AnalyticsEndpoints(
        IAnalyticsService analyticsService,
        ILogger<AnalyticsEndpoints> logger,
        IHttpContextAccessor httpContextAccessor,
        IAnalyticsMetrics? analyticsMetrics = null)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _analyticsMetrics = analyticsMetrics;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/Analytics")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Analytics");
        group.MapGet("/logs", ([FromServices] AnalyticsEndpoints e, int page = 1, int pageSize = 50, DateTime? startDate = null, DateTime? endDate = null, string? model = null, int? virtualKeyId = null, int? status = null) => e.GetLogs(page, pageSize, startDate, endDate, model, virtualKeyId, status))
            .WithName("Analytics_GetLogs").Produces<PagedResult<LogRequestDto>>().Produces(StatusCodes.Status400BadRequest);
        group.MapGet("/logs/{id:int}", ([FromServices] AnalyticsEndpoints e, int id) => e.GetLogById(id))
            .WithName("Analytics_GetLogById").Produces<LogRequestDto>().Produces(StatusCodes.Status404NotFound);
        group.MapGet("/logs/models", ([FromServices] AnalyticsEndpoints e) => e.GetDistinctModels())
            .WithName("Analytics_GetDistinctModels").Produces<IEnumerable<string>>();
        group.MapGet("/costs/summary", ([FromServices] AnalyticsEndpoints e, string timeframe = "daily", DateTime? startDate = null, DateTime? endDate = null) => e.GetCostSummary(timeframe, startDate, endDate))
            .WithName("Analytics_GetCostSummary").Produces<CostDashboardDto>().Produces(StatusCodes.Status400BadRequest);
        group.MapGet("/costs/trends", ([FromServices] AnalyticsEndpoints e, string period = "daily", DateTime? startDate = null, DateTime? endDate = null) => e.GetCostTrends(period, startDate, endDate))
            .WithName("Analytics_GetCostTrends").Produces<CostTrendDto>().Produces(StatusCodes.Status400BadRequest);
        group.MapGet("/costs/models", ([FromServices] AnalyticsEndpoints e, DateTime? startDate = null, DateTime? endDate = null, int topN = 10) => e.GetModelCosts(startDate, endDate, topN))
            .WithName("Analytics_GetModelCosts").Produces<ModelCostBreakdownDto>();
        group.MapGet("/costs/virtualkeys", ([FromServices] AnalyticsEndpoints e, DateTime? startDate = null, DateTime? endDate = null, int topN = 10) => e.GetVirtualKeyCosts(startDate, endDate, topN))
            .WithName("Analytics_GetVirtualKeyCosts").Produces<VirtualKeyCostBreakdownDto>();
        group.MapGet("/summary", ([FromServices] AnalyticsEndpoints e, string timeframe = "daily", DateTime? startDate = null, DateTime? endDate = null) => e.GetAnalyticsSummary(timeframe, startDate, endDate))
            .WithName("Analytics_GetAnalyticsSummary").Produces<AnalyticsSummaryDto>().Produces(StatusCodes.Status400BadRequest);
        group.MapGet("/virtualkeys/{virtualKeyId:int}/usage", ([FromServices] AnalyticsEndpoints e, int virtualKeyId, DateTime? startDate = null, DateTime? endDate = null) => e.GetVirtualKeyUsage(virtualKeyId, startDate, endDate))
            .WithName("Analytics_GetVirtualKeyUsage").Produces<UsageStatisticsDto>();
        group.MapGet("/export", ([FromServices] AnalyticsEndpoints e, string format = "csv", DateTime? startDate = null, DateTime? endDate = null, string? model = null, int? virtualKeyId = null) => e.ExportAnalytics(format, startDate, endDate, model, virtualKeyId))
            .WithName("Analytics_ExportAnalytics").Produces(StatusCodes.Status200OK, typeof(void), "text/csv", "application/json").Produces(StatusCodes.Status400BadRequest);
        group.MapGet("/metrics/cache", ([FromServices] AnalyticsEndpoints e) => e.GetCacheMetrics())
            .WithName("Analytics_GetCacheMetrics").Produces<AnalyticsCacheMetricsResponse>().Produces(StatusCodes.Status404NotFound);
        group.MapGet("/metrics/operations", ([FromServices] AnalyticsEndpoints e) => e.GetOperationMetrics())
            .WithName("Analytics_GetOperationMetrics").Produces<Dictionary<string, double>>().Produces(StatusCodes.Status404NotFound);
        group.MapPost("/cache/invalidate", ([FromServices] AnalyticsEndpoints e, string reason = "Manual invalidation") => e.InvalidateCache(reason))
            .WithName("Analytics_InvalidateCache").Produces<AnalyticsCacheInvalidationResponse>();
        return app;
    }

    #region Request Logs

    /// <summary>
    /// Gets paginated request logs
    /// </summary>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="startDate">Optional filter by start date</param>
    /// <param name="endDate">Optional filter by end date</param>
    /// <param name="model">Optional filter by model</param>
    /// <param name="virtualKeyId">Optional filter by virtual key ID</param>
    /// <param name="status">Optional filter by status code</param>
    /// <returns>A paged result containing the request logs</returns>
    public async Task<IResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? model = null,
        [FromQuery] int? virtualKeyId = null,
        [FromQuery] int? status = null)
    {
        // Validate parameters
        if (page < 1)
        {
            return AdminResults.BadRequest("Page must be greater than or equal to 1");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return AdminResults.BadRequest("Page size must be between 1 and 100");
        }

        var result = await _analyticsService.GetLogsAsync(
            page, pageSize, startDate, endDate, model, virtualKeyId, status);
        return Results.Ok(result);
    }

    /// <summary>
    /// Gets a single log entry by ID
    /// </summary>
    /// <param name="id">The ID of the log to retrieve</param>
    /// <returns>The log entry</returns>
    public async Task<IResult> GetLogById(int id)
    {
        var log = await _analyticsService.GetLogByIdAsync(id);
        if (log == null)
        {
            return AdminResults.NotFoundEntity("Log entry", id);
        }
        return Results.Ok(log);
    }

    /// <summary>
    /// Gets a list of distinct model names from request logs
    /// </summary>
    /// <returns>List of model names</returns>
    public async Task<IResult> GetDistinctModels()
    {
        var models = await _analyticsService.GetDistinctModelsAsync();
        return Results.Ok(models);
    }

    #endregion

    #region Cost Analytics

    /// <summary>
    /// Gets cost dashboard summary data
    /// </summary>
    /// <param name="timeframe">The timeframe for the summary (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the summary</param>
    /// <param name="endDate">The end date for the summary</param>
    /// <returns>The cost dashboard summary</returns>
    public async Task<IResult> GetCostSummary(
        [FromQuery] string timeframe = "daily",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (timeframe is not ("daily" or "weekly" or "monthly"))
        {
            return AdminResults.BadRequest("Timeframe must be one of: daily, weekly, monthly");
        }

        var summary = await _analyticsService.GetCostSummaryAsync(timeframe, startDate, endDate);
        return Results.Ok(summary);
    }

    /// <summary>
    /// Gets cost trend data
    /// </summary>
    /// <param name="period">The period for the trend (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the trend</param>
    /// <param name="endDate">The end date for the trend</param>
    /// <returns>The cost trend data</returns>
    public async Task<IResult> GetCostTrends(
        [FromQuery] string period = "daily",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (period is not ("daily" or "weekly" or "monthly"))
        {
            return AdminResults.BadRequest("Period must be one of: daily, weekly, monthly");
        }

        var trends = await _analyticsService.GetCostTrendsAsync(period, startDate, endDate);
        return Results.Ok(trends);
    }

    /// <summary>
    /// Gets costs grouped by model
    /// </summary>
    /// <param name="startDate">The start date for the analysis</param>
    /// <param name="endDate">The end date for the analysis</param>
    /// <param name="topN">Number of top models to return</param>
    /// <returns>Model cost breakdown</returns>
    public async Task<IResult> GetModelCosts(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int topN = 10)
    {
        var modelCosts = await _analyticsService.GetModelCostsAsync(startDate, endDate, topN);
        return Results.Ok(modelCosts);
    }

    /// <summary>
    /// Gets costs grouped by virtual key
    /// </summary>
    /// <param name="startDate">The start date for the analysis</param>
    /// <param name="endDate">The end date for the analysis</param>
    /// <param name="topN">Number of top virtual keys to return</param>
    /// <returns>Virtual key cost breakdown</returns>
    public async Task<IResult> GetVirtualKeyCosts(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int topN = 10)
    {
        var virtualKeyCosts = await _analyticsService.GetVirtualKeyCostsAsync(startDate, endDate, topN);
        return Results.Ok(virtualKeyCosts);
    }

    #endregion

    #region Combined Analytics

    /// <summary>
    /// Gets comprehensive analytics summary
    /// </summary>
    /// <param name="timeframe">The timeframe for the summary (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the summary</param>
    /// <param name="endDate">The end date for the summary</param>
    /// <returns>Analytics summary</returns>
    public async Task<IResult> GetAnalyticsSummary(
        [FromQuery] string timeframe = "daily",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (timeframe is not ("daily" or "weekly" or "monthly"))
        {
            return AdminResults.BadRequest("Timeframe must be one of: daily, weekly, monthly");
        }

        var summary = await _analyticsService.GetAnalyticsSummaryAsync(timeframe, startDate, endDate);
        return Results.Ok(summary);
    }

    /// <summary>
    /// Gets usage statistics for a specific virtual key
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <param name="startDate">The start date for the analysis</param>
    /// <param name="endDate">The end date for the analysis</param>
    /// <returns>Usage statistics</returns>
    public async Task<IResult> GetVirtualKeyUsage(
        int virtualKeyId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var usage = await _analyticsService.GetVirtualKeyUsageAsync(virtualKeyId, startDate, endDate);
        return Results.Ok(usage);
    }

    /// <summary>
    /// Exports analytics data
    /// </summary>
    /// <param name="format">Export format (csv, json)</param>
    /// <param name="startDate">The start date for the export</param>
    /// <param name="endDate">The end date for the export</param>
    /// <param name="model">Optional model filter</param>
    /// <param name="virtualKeyId">Optional virtual key filter</param>
    /// <returns>Exported data file</returns>
    public async Task<IResult> ExportAnalytics(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? model = null,
        [FromQuery] int? virtualKeyId = null)
    {
        // Validate format
        if (format.ToLower() != "csv" && format.ToLower() != "json")
        {
            return AdminResults.BadRequest("Format must be one of: csv, json");
        }

        using var activity = AdminRequestMetrics.StartCsvActivity("export", "analytics");
        var data = await _analyticsService.ExportAnalyticsAsync(format, startDate, endDate, model, virtualKeyId);

        var contentType = format.ToLower() == "csv" ? "text/csv" : "application/json";
        var fileName = $"analytics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format.ToLower()}";

        LogAdminAudit("Exported", "AnalyticsData", detail: $"Format: {format}, StartDate: {startDate:O}, EndDate: {endDate:O}");
        AdminOperationsMetricsService.RecordCsvOperation("export", "analytics", "success");
        return Results.File(data, contentType, fileName);
    }

    #endregion

    #region Metrics

    /// <summary>
    /// Gets analytics cache metrics
    /// </summary>
    /// <returns>Cache metrics including hit rates and memory usage</returns>
    public IResult GetCacheMetrics()
    {
        if (_analyticsMetrics == null)
        {
            return AdminResults.NotFound("Metrics collection is not enabled");
        }

        var metrics = _analyticsMetrics.GetCacheStatistics();
        return Results.Ok(new AnalyticsCacheMetricsResponse(
            Convert.ToInt64(metrics["TotalHits"]),
            Convert.ToInt64(metrics["TotalMisses"]),
            Convert.ToDouble(metrics["HitRate"]),
            Convert.ToDouble(metrics["CacheMemoryMB"]),
            Convert.ToInt64(metrics["TotalInvalidations"]),
            Convert.ToDouble(metrics["UptimeMinutes"]),
            ToMetricCounts(metrics["TopHitKeys"]),
            ToMetricCounts(metrics["TopMissKeys"])));
    }

    /// <summary>
    /// Gets analytics operation performance metrics
    /// </summary>
    /// <returns>Operation performance metrics including P95 and average durations</returns>
    public IResult GetOperationMetrics()
    {
        if (_analyticsMetrics == null)
        {
            return AdminResults.NotFound("Metrics collection is not enabled");
        }

        var metrics = _analyticsMetrics.GetOperationStatistics();
        return Results.Ok(metrics);
    }

    /// <summary>
    /// Invalidates analytics cache
    /// </summary>
    /// <param name="reason">Reason for cache invalidation</param>
    /// <returns>Success response</returns>
    public IResult InvalidateCache(string reason = "Manual invalidation")
    {
        var keysInvalidated = _analyticsService.InvalidateCache();
        _analyticsMetrics?.RecordCacheInvalidation(reason, keysInvalidated);
        LogAdminAudit(
            "Invalidated",
            "AnalyticsCache",
            detail: $"Reason: {reason}, KeysInvalidated: {keysInvalidated}");
        return Results.Ok(new AnalyticsCacheInvalidationResponse(
            "Analytics cache invalidated",
            reason,
            keysInvalidated));
    }

    private void LogAdminAudit(string operation, string entityType, object? entityId = null, string? detail = null)
    {
        var context = _httpContextAccessor.HttpContext;
        _logger.LogInformation(
            "Admin Audit: {Operation} {EntityType} {EntityId} by {AdminUser} from {ClientIp} [TraceId: {TraceId}] - {Detail}",
            operation, entityType, entityId ?? "N/A", context?.User.Identity?.Name ?? "Unknown",
            context?.Connection.RemoteIpAddress?.ToString() ?? "unknown", context?.TraceIdentifier ?? "unknown", detail);
    }

    #endregion

    private static IReadOnlyList<MetricKeyCountDto> ToMetricCounts(object value) =>
        ((IEnumerable<KeyValuePair<string, long>>)value)
        .Select(pair => new MetricKeyCountDto(pair.Key, pair.Value))
        .ToList();
}
