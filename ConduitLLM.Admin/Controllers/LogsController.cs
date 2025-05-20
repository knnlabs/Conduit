using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing logs
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class LogsController : ControllerBase
{
    private readonly IAdminLogService _logService;
    private readonly ILogger<LogsController> _logger;
    
    /// <summary>
    /// Initializes a new instance of the LogsController
    /// </summary>
    /// <param name="logService">The log service</param>
    /// <param name="logger">The logger</param>
    public LogsController(
        IAdminLogService logService,
        ILogger<LogsController> logger)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
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
    [HttpGet]
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
            
            var logs = await _logService.GetLogsAsync(
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
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LogRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogById(int id)
    {
        try
        {
            var log = await _logService.GetLogByIdAsync(id);
            
            if (log == null)
            {
                return NotFound($"Log with ID {id} not found");
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
    /// Gets logs summarized by the specified timeframe
    /// </summary>
    /// <param name="timeframe">The timeframe for the summary (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the summary</param>
    /// <param name="endDate">The end date for the summary</param>
    /// <returns>The logs summary</returns>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(Configuration.Services.Dtos.LogsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogsSummary(
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
            
            var summary = await _logService.GetLogsSummaryAsync(timeframe, startDate, endDate);
            
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs summary");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}