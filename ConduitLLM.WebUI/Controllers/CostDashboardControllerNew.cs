using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Provides API endpoints for retrieving cost and usage data for the cost dashboard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller is part of the new repository-based architecture and provides
    /// endpoints for retrieving cost and usage data aggregated by different dimensions
    /// such as time period, virtual key, and model.
    /// </para>
    /// <para>
    /// The controller interfaces with the CostDashboardServiceNew which handles the
    /// business logic for calculating costs and aggregating usage data.
    /// </para>
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CostDashboardControllerNew : ControllerBase
    {
        private readonly CostDashboardServiceNew _costDashboardService;
        private readonly ILogger<CostDashboardControllerNew> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CostDashboardControllerNew"/> class.
        /// </summary>
        /// <param name="costDashboardService">The service for retrieving cost dashboard data.</param>
        /// <param name="logger">The logger instance.</param>
        public CostDashboardControllerNew(
            CostDashboardServiceNew costDashboardService,
            ILogger<CostDashboardControllerNew> logger)
        {
            _costDashboardService = costDashboardService ?? throw new ArgumentNullException(nameof(costDashboardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves cost dashboard data for the specified time period, virtual key, and model.
        /// </summary>
        /// <param name="startDate">The start date for the data (defaults to 30 days ago).</param>
        /// <param name="endDate">The end date for the data (defaults to current time).</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by.</param>
        /// <param name="modelName">Optional model name to filter by.</param>
        /// <returns>
        /// A JSON object containing aggregated cost and usage data for the dashboard.
        /// </returns>
        /// <response code="200">Returns the dashboard data object</response>
        /// <response code="500">If an error occurs while retrieving the data</response>
        [HttpGet("data")]
        public async Task<IActionResult> GetCostDashboardData(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? virtualKeyId = null,
            [FromQuery] string? modelName = null)
        {
            try
            {
                // Default to last 30 days if no dates provided
                if (!startDate.HasValue)
                    startDate = DateTime.UtcNow.AddDays(-30);
                
                if (!endDate.HasValue)
                    endDate = DateTime.UtcNow;

                var data = await _costDashboardService.GetDashboardDataAsync(
                    startDate,
                    endDate,
                    virtualKeyId,
                    modelName);

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost dashboard data");
                return StatusCode(500, "An error occurred while retrieving cost dashboard data");
            }
        }

        /// <summary>
        /// Retrieves cost trend data based on the specified period and filter criteria.
        /// </summary>
        /// <param name="period">The time period granularity ('day', 'week', or 'month').</param>
        /// <param name="count">The number of periods to include.</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by.</param>
        /// <param name="modelName">Optional model name to filter by.</param>
        /// <returns>
        /// A JSON object containing cost trend data aggregated by the specified period.
        /// </returns>
        /// <remarks>
        /// This endpoint calculates the appropriate date range based on the period and count parameters:
        /// - 'day': Returns data for the last N days
        /// - 'week': Returns data for the last N weeks
        /// - 'month': Returns data for the last N months
        /// </remarks>
        /// <response code="200">Returns the cost trend data</response>
        /// <response code="400">If the period type is invalid or count is out of range</response>
        /// <response code="500">If an error occurs while retrieving the data</response>
        [HttpGet("trend")]
        public async Task<IActionResult> GetCostTrend(
            [FromQuery] string period = "day",
            [FromQuery] int count = 30,
            [FromQuery] int? virtualKeyId = null,
            [FromQuery] string? modelName = null)
        {
            try
            {
                // Validate period
                if (!string.Equals(period, "day", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(period, "week", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(period, "month", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid period type. Must be 'day', 'week', or 'month'.");
                }

                // Validate count
                if (count <= 0 || count > 365)
                {
                    return BadRequest("Count must be between 1 and 365.");
                }

                // Calculate dates based on period and count
                DateTime endDate = DateTime.UtcNow;
                DateTime startDate;
                
                switch (period.ToLower())
                {
                    case "week":
                        startDate = endDate.AddDays(-7 * count);
                        break;
                    case "month":
                        startDate = endDate.AddMonths(-count);
                        break;
                    case "day":
                    default:
                        startDate = endDate.AddDays(-count);
                        break;
                }
                
                var data = await _costDashboardService.GetDashboardDataAsync(
                    startDate,
                    endDate,
                    virtualKeyId,
                    modelName);

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost trend data");
                return StatusCode(500, "An error occurred while retrieving cost trend data");
            }
        }
    }
}