using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Costs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for cost dashboard
/// </summary>
[ApiController]
[Route("api/costs")]
[Authorize(Policy = "MasterKeyPolicy")]
public class CostDashboardController : ControllerBase
{
    private readonly IAdminCostDashboardService _costDashboardService;
    private readonly ILogger<CostDashboardController> _logger;
    
    /// <summary>
    /// Initializes a new instance of the CostDashboardController
    /// </summary>
    /// <param name="costDashboardService">The cost dashboard service</param>
    /// <param name="logger">The logger</param>
    public CostDashboardController(
        IAdminCostDashboardService costDashboardService,
        ILogger<CostDashboardController> logger)
    {
        _costDashboardService = costDashboardService ?? throw new ArgumentNullException(nameof(costDashboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Gets cost dashboard summary data
    /// </summary>
    /// <param name="timeframe">The timeframe for the summary (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the summary</param>
    /// <param name="endDate">The end date for the summary</param>
    /// <returns>The cost dashboard summary</returns>
    [HttpGet("summary")]
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
            
            var summary = await _costDashboardService.GetCostSummaryAsync(timeframe, startDate, endDate);
            
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
    [HttpGet("trends")]
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
            
            var trends = await _costDashboardService.GetCostTrendsAsync(period, startDate, endDate);
            
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost trends");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
    
    /// <summary>
    /// Gets model costs data
    /// </summary>
    /// <param name="startDate">The start date for the data</param>
    /// <param name="endDate">The end date for the data</param>
    /// <returns>The model costs data</returns>
    [HttpGet("models")]
    [ProducesResponseType(typeof(List<ModelCostDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetModelCosts(
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var models = await _costDashboardService.GetModelCostsAsync(startDate, endDate);
            
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model costs");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
    
    /// <summary>
    /// Gets virtual key costs data
    /// </summary>
    /// <param name="startDate">The start date for the data</param>
    /// <param name="endDate">The end date for the data</param>
    /// <returns>The virtual key costs data</returns>
    [HttpGet("virtualkeys")]
    [ProducesResponseType(typeof(List<VirtualKeyCostDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVirtualKeyCosts(
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var virtualKeys = await _costDashboardService.GetVirtualKeyCostsAsync(startDate, endDate);
            
            return Ok(virtualKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key costs");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}