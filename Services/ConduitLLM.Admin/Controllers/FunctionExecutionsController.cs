using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for monitoring and managing function executions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class FunctionExecutionsController : ControllerBase
{
    private readonly IFunctionExecutionRepository _executionRepository;
    private readonly ILogger<FunctionExecutionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the FunctionExecutionsController.
    /// </summary>
    public FunctionExecutionsController(
        IFunctionExecutionRepository executionRepository,
        ILogger<FunctionExecutionsController> logger)
    {
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets an execution by ID.
    /// </summary>
    /// <param name="id">The execution ID</param>
    /// <returns>The execution</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExecutionById(Guid id)
    {
        try
        {
            var execution = await _executionRepository.GetByIdAsync(id);

            if (execution == null)
            {
                return NotFound(new ErrorResponseDto("Function execution not found"));
            }

            var dto = MapToDto(execution);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function execution with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets executions for a virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <returns>List of executions</returns>
    [HttpGet("virtualkey/{virtualKeyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExecutionsByVirtualKey(int virtualKeyId)
    {
        try
        {
            var executions = await _executionRepository.GetByVirtualKeyIdAsync(virtualKeyId);
            var dtos = executions.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting function executions for virtual key {VirtualKeyId}",
                virtualKeyId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets executions for a function configuration.
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <returns>List of executions</returns>
    [HttpGet("configuration/{functionConfigurationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExecutionsByConfiguration(int functionConfigurationId)
    {
        try
        {
            var executions = await _executionRepository.GetByFunctionConfigurationIdAsync(
                functionConfigurationId);
            var dtos = executions.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting function executions for configuration {FunctionConfigurationId}",
                functionConfigurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets executions by state.
    /// </summary>
    /// <param name="state">The execution state (e.g., "Pending", "Running", "Completed", "Failed")</param>
    /// <returns>List of executions in the specified state</returns>
    [HttpGet("state/{state}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExecutionsByState(string state)
    {
        try
        {
            if (!Enum.TryParse<ExecutionState>(state, true, out var stateEnum))
            {
                return BadRequest(new ErrorResponseDto($"Invalid execution state: {state}"));
            }

            var executions = await _executionRepository.GetByStateAsync(stateEnum);
            var dtos = executions.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function executions for state {State}", state);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets executions with expired leases.
    /// </summary>
    /// <returns>List of executions with expired leases</returns>
    [HttpGet("expired-leases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExpiredLeases()
    {
        try
        {
            var executions = await _executionRepository.GetExpiredLeasesAsync();
            var dtos = executions.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function executions with expired leases");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets executions ready for retry.
    /// </summary>
    /// <returns>List of executions ready for retry</returns>
    [HttpGet("ready-for-retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReadyForRetry()
    {
        try
        {
            var executions = await _executionRepository.GetReadyForRetryAsync();
            var dtos = executions.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function executions ready for retry");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Deletes old executions.
    /// </summary>
    /// <param name="olderThanDays">Delete executions older than this many days</param>
    /// <returns>Number of executions deleted</returns>
    [HttpDelete("cleanup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CleanupOldExecutions([FromQuery] int olderThanDays = 30)
    {
        try
        {
            if (olderThanDays < 1)
            {
                return BadRequest(new ErrorResponseDto("olderThanDays must be at least 1"));
            }

            var olderThan = DateTime.UtcNow.AddDays(-olderThanDays);
            var deletedCount = await _executionRepository.DeleteOldExecutionsAsync(olderThan);

            return Ok(new
            {
                deletedCount,
                message = $"Deleted {deletedCount} executions older than {olderThanDays} days"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old function executions");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    // Mapping methods

    /// <summary>
    /// Maps FunctionExecution entity to DTO, converting TimeSpan to milliseconds
    /// </summary>
    private static FunctionExecutionDto MapToDto(FunctionExecution entity)
    {
        return new FunctionExecutionDto
        {
            Id = entity.Id,
            FunctionConfigurationId = entity.FunctionConfigurationId,
            VirtualKeyId = entity.VirtualKeyId,
            ExecutionMode = entity.ExecutionMode,
            State = entity.State,
            RequestedAt = entity.RequestedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            Duration = entity.Duration?.TotalMilliseconds,
            RequestJson = entity.RequestJson,
            ResponseJson = entity.ResponseJson,
            ErrorMessage = entity.ErrorMessage,
            EstimatedCost = entity.EstimatedCost,
            ActualCost = entity.ActualCost,
            CostCalculationDetails = entity.CostCalculationDetails,
            RetryCount = entity.RetryCount,
            NextRetryAt = entity.NextRetryAt,
            LeasedBy = entity.LeasedBy,
            LeaseExpiryTime = entity.LeaseExpiryTime,
            Version = entity.Version,
            WebhookUrl = entity.WebhookUrl,
            WebhookDelivered = entity.WebhookDelivered,
            ProgressPercentage = entity.ProgressPercentage,
            StatusMessage = entity.StatusMessage
        };
    }
}
