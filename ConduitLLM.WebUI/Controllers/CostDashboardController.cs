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
    /// Provides endpoints for retrieving cost data for the dashboard.
    /// </summary>
    /// <remarks>
    /// This controller provides API endpoints for retrieving cost data to be displayed
    /// in the Cost Dashboard UI. It supports filtering data by date range, virtual key,
    /// and model name, as well as retrieving trend data over different time periods.
    /// All endpoints require authentication.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CostDashboardController : ControllerBase
    {
        private readonly ICostDashboardService _costDashboardService;
        private readonly ILogger<CostDashboardController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CostDashboardController"/> class.
        /// </summary>
        /// <param name="costDashboardService">The service for retrieving cost dashboard data.</param>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        public CostDashboardController(
            ICostDashboardService costDashboardService,
            ILogger<CostDashboardController> logger)
        {
            _costDashboardService = costDashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves cost dashboard data for the specified parameters.
        /// </summary>
        /// <param name="startDate">The start date for the data range. Defaults to 30 days ago if not specified.</param>
        /// <param name="endDate">The end date for the data range. Defaults to the current date if not specified.</param>
        /// <param name="virtualKeyId">Optional filter for a specific virtual key.</param>
        /// <param name="modelName">Optional filter for a specific model name.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the dashboard data.
        /// Returns:
        /// - 200 OK with the dashboard data if successful
        /// - 500 Internal Server Error if an unexpected error occurs
        /// </returns>
        /// <remarks>
        /// This endpoint retrieves cost data for the dashboard, including summaries by model,
        /// virtual key, and time period. The data can be filtered by date range, virtual key,
        /// and model name.
        /// </remarks>
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
        /// Retrieves cost trend data for the specified period and count.
        /// </summary>
        /// <param name="period">The period type for the trend (day, week, or month). Defaults to "day".</param>
        /// <param name="count">The number of periods to include. Defaults to 30.</param>
        /// <param name="virtualKeyId">Optional filter for a specific virtual key.</param>
        /// <param name="modelName">Optional filter for a specific model name.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the trend data.
        /// Returns:
        /// - 200 OK with the trend data if successful
        /// - 400 Bad Request if the period or count is invalid
        /// - 500 Internal Server Error if an unexpected error occurs
        /// </returns>
        /// <remarks>
        /// This endpoint calculates the date range based on the specified period type and count,
        /// then retrieves the cost data for that range. The period can be "day", "week", or "month",
        /// and the count must be between 1 and 365.
        /// </remarks>
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
