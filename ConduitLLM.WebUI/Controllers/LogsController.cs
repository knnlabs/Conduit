using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving and querying request logs.
    /// </summary>
    /// <remarks>
    /// This controller provides API endpoints for searching and retrieving request logs,
    /// as well as summary data about logs. It supports filtering logs by various criteria
    /// such as virtual key, model, date range, and status code. All endpoints require 
    /// authentication.
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
        /// <param name="requestLogService">The service for retrieving request logs.</param>
        /// <param name="virtualKeyService">The service for retrieving virtual keys.</param>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        public LogsController(
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            ILogger<LogsController> logger)
        {
            _requestLogService = requestLogService;
            _virtualKeyService = virtualKeyService;
            _logger = logger;
        }

        /// <summary>
        /// Searches for request logs based on the specified criteria.
        /// </summary>
        /// <param name="virtualKeyId">Optional filter for a specific virtual key.</param>
        /// <param name="modelFilter">Optional filter for a specific model.</param>
        /// <param name="startDate">The start date for the search range. Defaults to 24 hours ago if not specified.</param>
        /// <param name="endDate">The end date for the search range. Defaults to the current date if not specified.</param>
        /// <param name="statusCode">Optional filter for a specific HTTP status code.</param>
        /// <param name="page">The page number to retrieve. Defaults to 1.</param>
        /// <param name="pageSize">The number of items per page. Defaults to 20, maximum 100.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the search results as a paged collection.
        /// Returns:
        /// - 200 OK with the search results if successful
        /// - 500 Internal Server Error if an unexpected error occurs
        /// </returns>
        /// <remarks>
        /// This endpoint searches for request logs matching the specified criteria and returns
        /// a paged collection of results. It includes information about each log entry, such as
        /// the virtual key used, model name, token counts, cost, and response time.
        /// </remarks>
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
                var logDtos = result.Logs.Select(l => new ConduitLLM.WebUI.DTOs.RequestLogDto
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

                return Ok(new ConduitLLM.WebUI.DTOs.PagedResult<ConduitLLM.WebUI.DTOs.RequestLogDto>
                {
                    Items = logDtos,
                    TotalCount = result.TotalCount,
                    PageNumber = page,
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
        /// Retrieves a summary of request logs for the specified date range.
        /// </summary>
        /// <param name="startDate">The start date for the summary range. Defaults to 7 days ago if not specified.</param>
        /// <param name="endDate">The end date for the summary range. Defaults to the current date if not specified.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the logs summary.
        /// Returns:
        /// - 200 OK with the logs summary if successful
        /// - 500 Internal Server Error if an unexpected error occurs
        /// </returns>
        /// <remarks>
        /// This endpoint retrieves a summary of request logs for the specified date range,
        /// including aggregated statistics such as total requests, total cost, total tokens,
        /// and average response time.
        /// </remarks>
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
        /// Retrieves a list of available virtual keys.
        /// </summary>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the list of virtual keys.
        /// Returns:
        /// - 200 OK with the list of virtual keys if successful
        /// - 500 Internal Server Error if an unexpected error occurs
        /// </returns>
        /// <remarks>
        /// This endpoint retrieves a list of all available virtual keys in the system,
        /// returning their IDs and names. This is typically used for populating filter
        /// dropdown lists in the UI.
        /// </remarks>
        [HttpGet("keys")]
        public async Task<IActionResult> GetVirtualKeys()
        {
            try
            {
                var keys = await _virtualKeyService.GetAllVirtualKeysAsync();
                return Ok(keys.Select(k => new { k.Id, k.KeyName }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual keys");
                return StatusCode(500, "An error occurred while getting virtual keys");
            }
        }

        /// <summary>
        /// Retrieves a list of distinct model names from the logs.
        /// </summary>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the list of model names.
        /// Returns:
        /// - 200 OK with the list of model names if successful
        /// - 500 Internal Server Error if an unexpected error occurs
        /// </returns>
        /// <remarks>
        /// This endpoint retrieves a list of all distinct model names found in the request logs.
        /// This is typically used for populating filter dropdown lists in the UI.
        /// </remarks>
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
