using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CostDashboardController : ControllerBase
    {
        private readonly ICostDashboardService _costDashboardService;
        private readonly ILogger<CostDashboardController> _logger;

        public CostDashboardController(
            ICostDashboardService costDashboardService,
            ILogger<CostDashboardController> logger)
        {
            _costDashboardService = costDashboardService;
            _logger = logger;
        }

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

                var data = await _costDashboardService.GetCostDashboardDataAsync(
                    startDate.Value,
                    endDate.Value,
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

                var data = await _costDashboardService.GetCostTrendAsync(
                    period,
                    count,
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
