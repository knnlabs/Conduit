using ConduitLLM.Admin.Extensions;
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetExecutionById(Guid id)
    {
        return ExecuteWithNotFoundAsync(
            async () =>
            {
                var execution = await _executionRepository.GetByIdAsync(id);
                return execution?.ToDto();
            },
            Ok,
            "Function execution",
            id,
            "GetExecutionById");
    }

    /// <summary>
    /// Gets executions for a virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <returns>List of executions</returns>
    [HttpGet("virtualkey/{virtualKeyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetExecutionsByVirtualKey(int virtualKeyId)
    {
        return ExecuteAsync(
            async () =>
            {
                var executions = await _executionRepository.GetByVirtualKeyIdAsync(virtualKeyId);
                return executions.Select(e => e.ToDto()).ToList();
            },
            Ok,
            "GetExecutionsByVirtualKey",
            new { VirtualKeyId = virtualKeyId });
    }

    /// <summary>
    /// Gets executions for a function configuration.
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <returns>List of executions</returns>
    [HttpGet("configuration/{functionConfigurationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetExecutionsByConfiguration(int functionConfigurationId)
    {
        return ExecuteAsync(
            async () =>
            {
                var executions = await _executionRepository.GetByFunctionConfigurationIdAsync(
                    functionConfigurationId);
                return executions.Select(e => e.ToDto()).ToList();
            },
            Ok,
            "GetExecutionsByConfiguration",
            new { FunctionConfigurationId = functionConfigurationId });
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
    public Task<IActionResult> GetExecutionsByState(string state)
    {
        if (!Enum.TryParse<ExecutionState>(state, true, out var stateEnum))
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto($"Invalid execution state: {state}")));
        }

        return ExecuteAsync(
            async () =>
            {
                var executions = await _executionRepository.GetByStateAsync(stateEnum);
                return executions.Select(e => e.ToDto()).ToList();
            },
            Ok,
            "GetExecutionsByState",
            new { State = state });
    }

    /// <summary>
    /// Gets executions with expired leases.
    /// </summary>
    /// <returns>List of executions with expired leases</returns>
    [HttpGet("expired-leases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetExpiredLeases()
    {
        return ExecuteAsync(
            async () =>
            {
                var executions = await _executionRepository.GetExpiredLeasesAsync();
                return executions.Select(e => e.ToDto()).ToList();
            },
            Ok,
            "GetExpiredLeases");
    }

    /// <summary>
    /// Gets executions ready for retry.
    /// </summary>
    /// <returns>List of executions ready for retry</returns>
    [HttpGet("ready-for-retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetReadyForRetry()
    {
        return ExecuteAsync(
            async () =>
            {
                var executions = await _executionRepository.GetReadyForRetryAsync();
                return executions.Select(e => e.ToDto()).ToList();
            },
            Ok,
            "GetReadyForRetry");
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
    public Task<IActionResult> CleanupOldExecutions([FromQuery] int olderThanDays = 30)
    {
        if (olderThanDays < 1)
        {
            return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("olderThanDays must be at least 1")));
        }

        return ExecuteAsync(
            async () =>
            {
                var olderThan = DateTime.UtcNow.AddDays(-olderThanDays);
                var deletedCount = await _executionRepository.DeleteOldExecutionsAsync(olderThan);

                return new
                {
                    deletedCount,
                    message = $"Deleted {deletedCount} executions older than {olderThanDays} days"
                };
            },
            Ok,
            "CleanupOldExecutions",
            new { OlderThanDays = olderThanDays });
    }

}
