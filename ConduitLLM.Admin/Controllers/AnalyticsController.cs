using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Unified controller for analytics, logs, and cost data
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IAnalyticsMetrics? _analyticsMetrics;
    private readonly ILogger<AnalyticsController> _logger;

    /// <summary>
    /// Initializes a new instance of the AnalyticsController
    /// </summary>
    /// <param name="analyticsService">The analytics service</param>
    /// <param name="analyticsMetrics">The analytics metrics service (optional)</param>
    /// <param name="logger">The logger</param>
    public AnalyticsController(
        IAnalyticsService analyticsService,
        ILogger<AnalyticsController> logger,
        IAnalyticsMetrics? analyticsMetrics = null)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _analyticsMetrics = analyticsMetrics;
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
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResult<LogRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? model = null,
        [FromQuery] int? virtualKeyId = null,
        [FromQuery] int? status = null)
    {
        try
        {
            // Validate parameters
            if (page < 1)
            {
                return BadRequest("Page must be greater than or equal to 1");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Page size must be between 1 and 100");
            }

            var logs = await _analyticsService.GetLogsAsync(
                page, pageSize, startDate, endDate, model, virtualKeyId, status);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets a single log entry by ID
    /// </summary>
    /// <param name="id">The ID of the log to retrieve</param>
    /// <returns>The log entry</returns>
    [HttpGet("logs/{id:int}")]
    [ProducesResponseType(typeof(LogRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogById(int id)
    {
        try
        {
            var log = await _analyticsService.GetLogByIdAsync(id);

            if (log == null)
            {
                return NotFound("Log entry not found");
            }

            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets a list of distinct model names from request logs
    /// </summary>
    /// <returns>List of model names</returns>
    [HttpGet("logs/models")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDistinctModels()
    {
        try
        {
            var models = await _analyticsService.GetDistinctModelsAsync();
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distinct models");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    [HttpGet("costs/summary")]
    [ProducesResponseType(typeof(CostDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCostSummary(
        [FromQuery] string timeframe = "daily",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Validate timeframe
            if (timeframe.ToLower() != "daily" && timeframe.ToLower() != "weekly" && timeframe.ToLower() != "monthly")
            {
                return BadRequest("Timeframe must be one of: daily, weekly, monthly");
            }

            var summary = await _analyticsService.GetCostSummaryAsync(timeframe, startDate, endDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost summary");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets cost trend data
    /// </summary>
    /// <param name="period">The period for the trend (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the trend</param>
    /// <param name="endDate">The end date for the trend</param>
    /// <returns>The cost trend data</returns>
    [HttpGet("costs/trends")]
    [ProducesResponseType(typeof(CostTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCostTrends(
        [FromQuery] string period = "daily",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Validate period
            if (period.ToLower() != "daily" && period.ToLower() != "weekly" && period.ToLower() != "monthly")
            {
                return BadRequest("Period must be one of: daily, weekly, monthly");
            }

            var trends = await _analyticsService.GetCostTrendsAsync(period, startDate, endDate);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost trends");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets costs grouped by model
    /// </summary>
    /// <param name="startDate">The start date for the analysis</param>
    /// <param name="endDate">The end date for the analysis</param>
    /// <param name="topN">Number of top models to return</param>
    /// <returns>Model cost breakdown</returns>
    [HttpGet("costs/models")]
    [ProducesResponseType(typeof(ModelCostBreakdownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetModelCosts(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int topN = 10)
    {
        try
        {
            var costs = await _analyticsService.GetModelCostsAsync(startDate, endDate, topN);
            return Ok(costs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model costs");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets costs grouped by virtual key
    /// </summary>
    /// <param name="startDate">The start date for the analysis</param>
    /// <param name="endDate">The end date for the analysis</param>
    /// <param name="topN">Number of top virtual keys to return</param>
    /// <returns>Virtual key cost breakdown</returns>
    [HttpGet("costs/virtualkeys")]
    [ProducesResponseType(typeof(VirtualKeyCostBreakdownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVirtualKeyCosts(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int topN = 10)
    {
        try
        {
            var costs = await _analyticsService.GetVirtualKeyCostsAsync(startDate, endDate, topN);
            return Ok(costs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key costs");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAnalyticsSummary(
        [FromQuery] string timeframe = "daily",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Validate timeframe
            if (timeframe.ToLower() != "daily" && timeframe.ToLower() != "weekly" && timeframe.ToLower() != "monthly")
            {
                return BadRequest("Timeframe must be one of: daily, weekly, monthly");
            }

            var summary = await _analyticsService.GetAnalyticsSummaryAsync(timeframe, startDate, endDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analytics summary");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets usage statistics for a specific virtual key
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <param name="startDate">The start date for the analysis</param>
    /// <param name="endDate">The end date for the analysis</param>
    /// <returns>Usage statistics</returns>
    [HttpGet("virtualkeys/{virtualKeyId:int}/usage")]
    [ProducesResponseType(typeof(UsageStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVirtualKeyUsage(
        int virtualKeyId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var usage = await _analyticsService.GetVirtualKeyUsageAsync(virtualKeyId, startDate, endDate);
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key usage for ID {VirtualKeyId}", virtualKeyId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportAnalytics(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? model = null,
        [FromQuery] int? virtualKeyId = null)
    {
        try
        {
            // Validate format
            if (format.ToLower() != "csv" && format.ToLower() != "json")
            {
                return BadRequest("Format must be one of: csv, json");
            }

            var data = await _analyticsService.ExportAnalyticsAsync(format, startDate, endDate, model, virtualKeyId);
            
            var contentType = format.ToLower() == "csv" ? "text/csv" : "application/json";
            var fileName = $"analytics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format.ToLower()}";
            
            return File(data, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting analytics");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    #endregion

    #region Metrics

    /// <summary>
    /// Gets analytics cache metrics
    /// </summary>
    /// <returns>Cache metrics including hit rates and memory usage</returns>
    [HttpGet("metrics/cache")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetCacheMetrics()
    {
        if (_analyticsMetrics == null)
        {
            return NotFound("Metrics collection is not enabled");
        }

        var metrics = _analyticsMetrics.GetCacheStatistics();
        return Ok(metrics);
    }

    /// <summary>
    /// Gets analytics operation performance metrics
    /// </summary>
    /// <returns>Operation performance metrics including P95 and average durations</returns>
    [HttpGet("metrics/operations")]
    [ProducesResponseType(typeof(Dictionary<string, double>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetOperationMetrics()
    {
        if (_analyticsMetrics == null)
        {
            return NotFound("Metrics collection is not enabled");
        }

        var metrics = _analyticsMetrics.GetOperationStatistics();
        return Ok(metrics);
    }

    /// <summary>
    /// Invalidates analytics cache
    /// </summary>
    /// <param name="reason">Reason for cache invalidation</param>
    /// <returns>Success response</returns>
    [HttpPost("cache/invalidate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult InvalidateCache([FromQuery] string reason = "Manual invalidation")
    {
        try
        {
            // TODO: Implement cache invalidation logic
            _analyticsMetrics?.RecordCacheInvalidation(reason, 0);
            _logger.LogInformation("Cache invalidation requested: {Reason}", reason);
            return Ok(new { message = "Cache invalidation initiated", reason });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    #endregion
}