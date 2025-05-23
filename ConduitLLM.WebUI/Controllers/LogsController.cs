using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Provides API endpoints for retrieving and searching request logs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller provides endpoints for accessing request logs, including filtering, pagination, and
    /// retrieving summaries and metadata.
    /// </para>
    /// <para>
    /// Request logs track API usage including token counts, models used, costs,
    /// and performance metrics.
    /// </para>
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly IRequestLogService _requestLogService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<LogsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogsController"/> class.
        /// </summary>
        /// <param name="requestLogService">The service for accessing request logs.</param>
        /// <param name="virtualKeyService">The service for accessing virtual keys.</param>
        /// <param name="logger">The logger instance.</param>
        public LogsController(
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            ILogger<LogsController> logger)
        {
            _requestLogService = requestLogService ?? throw new ArgumentNullException(nameof(requestLogService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Searches for request logs based on various filter criteria.
        /// </summary>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by.</param>
        /// <param name="modelFilter">Optional model name to filter by.</param>
        /// <param name="startDate">The start date for the search (defaults to 24 hours ago).</param>
        /// <param name="endDate">The end date for the search (defaults to current time).</param>
        /// <param name="statusCode">Optional HTTP status code to filter by.</param>
        /// <param name="page">The page number for pagination (1-based).</param>
        /// <param name="pageSize">The number of items per page (max 100).</param>
        /// <returns>
        /// A paged collection of request log entries matching the filter criteria.
        /// </returns>
        /// <response code="200">Returns the matched log entries</response>
        /// <response code="500">If an error occurs while searching logs</response>
        [HttpGet("search")]
        public async Task<IActionResult> SearchLogs(
            [FromQuery] int? virtualKeyId = null,
            [FromQuery] string? modelFilter = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? statusCode = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100; // Prevent excessive page sizes

                // Default date range if not provided (last 24 hours)
                if (!startDate.HasValue)
                    startDate = DateTime.UtcNow.AddDays(-1);
                if (!endDate.HasValue)
                    endDate = DateTime.UtcNow;

                var result = await _requestLogService.SearchLogsAsync(
                    virtualKeyId,
                    modelFilter,
                    startDate.Value,
                    endDate.Value,
                    statusCode,
                    page,
                    pageSize);

                // Map to DTO
                var logDtos = result.Logs.Select(l => new RequestLogDto
                {
                    Id = l.Id,
                    VirtualKeyId = l.VirtualKeyId,
                    VirtualKeyName = l.VirtualKey?.KeyName ?? "Unknown",
                    ModelName = l.ModelName,
                    RequestType = l.RequestType,
                    InputTokens = l.InputTokens,
                    OutputTokens = l.OutputTokens,
                    Cost = l.Cost,
                    ResponseTimeMs = l.ResponseTimeMs,
                    Timestamp = l.Timestamp,
                    UserId = l.UserId,
                    ClientIp = l.ClientIp,
                    RequestPath = l.RequestPath,
                    StatusCode = l.StatusCode
                }).ToList();

                return Ok(new PagedResult<RequestLogDto>
                {
                    Items = logDtos,
                    TotalCount = result.TotalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching request logs");
                return StatusCode(500, "An error occurred while searching request logs");
            }
        }

        /// <summary>
        /// Retrieves a summary of request log data for the specified time period.
        /// </summary>
        /// <param name="startDate">The start date for the summary (defaults to 7 days ago).</param>
        /// <param name="endDate">The end date for the summary (defaults to current time).</param>
        /// <returns>
        /// A summary object containing aggregated metrics about request logs.
        /// </returns>
        /// <remarks>
        /// The summary includes metrics such as total requests, token usage, average response times,
        /// success rates, and cost information.
        /// </remarks>
        /// <response code="200">Returns the logs summary object</response>
        /// <response code="500">If an error occurs while retrieving the summary</response>
        [HttpGet("summary")]
        public async Task<IActionResult> GetLogsSummary(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Default date range if not provided (last 7 days)
                if (!startDate.HasValue)
                    startDate = DateTime.UtcNow.AddDays(-7);
                if (!endDate.HasValue)
                    endDate = DateTime.UtcNow;

                var summary = await _requestLogService.GetLogsSummaryAsync(startDate.Value, endDate.Value);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs summary");
                return StatusCode(500, "An error occurred while getting request logs summary");
            }
        }

        /// <summary>
        /// Retrieves a list of all virtual keys for filtering logs.
        /// </summary>
        /// <returns>
        /// A list of virtual key IDs and names.
        /// </returns>
        /// <remarks>
        /// This endpoint is used by the logs UI to populate filter dropdowns.
        /// </remarks>
        /// <response code="200">Returns the list of virtual keys</response>
        /// <response code="500">If an error occurs while retrieving the keys</response>
        [HttpGet("keys")]
        public async Task<IActionResult> GetVirtualKeys()
        {
            try
            {
                var keys = await _virtualKeyService.ListVirtualKeysAsync();
                return Ok(keys.Select(k => new { k.Id, k.KeyName }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual keys");
                return StatusCode(500, "An error occurred while getting virtual keys");
            }
        }

        /// <summary>
        /// Retrieves a list of all distinct model names that appear in the logs.
        /// </summary>
        /// <returns>
        /// A list of model names.
        /// </returns>
        /// <remarks>
        /// This endpoint is used by the logs UI to populate filter dropdowns.
        /// </remarks>
        /// <response code="200">Returns the list of distinct model names</response>
        /// <response code="500">If an error occurs while retrieving the models</response>
        [HttpGet("models")]
        public async Task<IActionResult> GetDistinctModels()
        {
            try
            {
                var models = await _requestLogService.GetDistinctModelsAsync();
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting distinct models");
                return StatusCode(500, "An error occurred while getting distinct models");
            }
        }
    }
}
