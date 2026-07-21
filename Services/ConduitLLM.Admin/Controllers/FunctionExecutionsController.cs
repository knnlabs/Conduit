using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.DTOs;
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
[ServiceFilter(typeof(OperationLoggingFilter))]
public class FunctionExecutionsController : AdminControllerBase
{
    private readonly IFunctionExecutionRepository _executionRepository;

    /// <summary>
    /// Initializes a new instance of the FunctionExecutionsController.
    /// </summary>
    public FunctionExecutionsController(
        IFunctionExecutionRepository executionRepository,
        ILogger<FunctionExecutionsController> logger)
        : base(logger)
    {
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
    }

    /// <summary>
    /// Gets an execution by ID.
    /// </summary>
    /// <param name="id">The execution ID</param>
    /// <returns>The execution</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FunctionExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionById(Guid id)
    {
        var execution = await _executionRepository.GetByIdAsync(id);
        if (execution == null)
        {
            return this.NotFoundEntity("Function execution", id);
        }

        return Ok(execution.ToDto());
    }

    /// <summary>
    /// Gets executions for a virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <returns>List of executions</returns>
    [HttpGet("virtualkey/{virtualKeyId}")]
    [ProducesResponseType(typeof(List<FunctionExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutionsByVirtualKey(int virtualKeyId)
    {
        var executions = await _executionRepository.GetByVirtualKeyIdAsync(virtualKeyId);
        return Ok(executions.Select(e => e.ToDto()).ToList());
    }

    /// <summary>
    /// Gets executions for a function configuration.
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <returns>List of executions</returns>
    [HttpGet("configuration/{functionConfigurationId}")]
    [ProducesResponseType(typeof(List<FunctionExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutionsByConfiguration(int functionConfigurationId)
    {
        var executions = await _executionRepository.GetByFunctionConfigurationIdAsync(
            functionConfigurationId);
        return Ok(executions.Select(e => e.ToDto()).ToList());
    }

    /// <summary>
    /// Gets executions by state.
    /// </summary>
    /// <param name="state">The execution state (e.g., "Pending", "Running", "Completed", "Failed")</param>
    /// <returns>List of executions in the specified state</returns>
    [HttpGet("state/{state}")]
    [ProducesResponseType(typeof(List<FunctionExecutionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetExecutionsByState(string state)
    {
        if (!Enum.TryParse<ExecutionState>(state, true, out var stateEnum))
        {
            return BadRequest(new ErrorResponseDto($"Invalid execution state: {state}"));
        }

        var executions = await _executionRepository.GetByStateAsync(stateEnum);
        return Ok(executions.Select(e => e.ToDto()).ToList());
    }

    /// <summary>
    /// Gets executions with expired leases.
    /// </summary>
    /// <returns>List of executions with expired leases</returns>
    [HttpGet("expired-leases")]
    [ProducesResponseType(typeof(List<FunctionExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiredLeases()
    {
        var executions = await _executionRepository.GetExpiredLeasesAsync();
        return Ok(executions.Select(e => e.ToDto()).ToList());
    }

    /// <summary>
    /// Gets executions ready for retry.
    /// </summary>
    /// <returns>List of executions ready for retry</returns>
    [HttpGet("ready-for-retry")]
    [ProducesResponseType(typeof(List<FunctionExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReadyForRetry()
    {
        var executions = await _executionRepository.GetReadyForRetryAsync();
        return Ok(executions.Select(e => e.ToDto()).ToList());
    }

    /// <summary>
    /// Deletes old executions.
    /// </summary>
    /// <param name="olderThanDays">Delete executions older than this many days</param>
    /// <returns>Number of executions deleted</returns>
    [HttpDelete("cleanup")]
    [ProducesResponseType(typeof(FunctionExecutionCleanupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CleanupOldExecutions([FromQuery] int olderThanDays = 30)
    {
        if (olderThanDays < 1)
        {
            return BadRequest(new ErrorResponseDto("olderThanDays must be at least 1"));
        }

        var olderThan = DateTime.UtcNow.AddDays(-olderThanDays);
        var deletedCount = await _executionRepository.DeleteOldExecutionsAsync(olderThan);

        LogAdminAudit("Cleanup", "FunctionExecution",
            detail: $"OlderThanDays: {olderThanDays}, DeletedCount: {deletedCount}");

        return Ok(new
        {
            deletedCount,
            message = $"Deleted {deletedCount} executions older than {olderThanDays} days"
        });
    }

}
