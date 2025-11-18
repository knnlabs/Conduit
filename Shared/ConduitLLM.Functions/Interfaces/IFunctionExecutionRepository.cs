using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Repository interface for managing function executions
/// </summary>
public interface IFunctionExecutionRepository
{
    /// <summary>
    /// Gets a function execution by ID
    /// </summary>
    /// <param name="id">The execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function execution or null if not found</returns>
    Task<FunctionExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all executions for a specific virtual key
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions</returns>
    Task<List<FunctionExecution>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all executions for a specific function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions</returns>
    Task<List<FunctionExecution>> GetByFunctionConfigurationIdAsync(int functionConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all executions with a specific state
    /// </summary>
    /// <param name="state">The execution state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions</returns>
    Task<List<FunctionExecution>> GetByStateAsync(ExecutionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leases the next pending execution for processing by a worker
    /// Atomically finds and leases an execution to prevent duplicate processing
    /// </summary>
    /// <param name="workerId">The worker instance ID</param>
    /// <param name="leaseDuration">How long the lease is valid</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The leased execution or null if no pending executions</returns>
    Task<FunctionExecution?> LeaseNextPendingAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all executions with expired leases (for recovery)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions with expired leases</returns>
    Task<List<FunctionExecution>> GetExpiredLeasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets executions that are ready for retry
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions ready for retry</returns>
    Task<List<FunctionExecution>> GetReadyForRetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new function execution
    /// </summary>
    /// <param name="execution">The execution to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created execution</returns>
    Task<Guid> CreateAsync(FunctionExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing function execution
    /// Handles optimistic concurrency via Version field
    /// </summary>
    /// <param name="execution">The execution to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update succeeded, false if version conflict</returns>
    Task<bool> UpdateAsync(FunctionExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates execution state
    /// </summary>
    /// <param name="executionId">The execution ID</param>
    /// <param name="state">The new state</param>
    /// <param name="errorMessage">Optional error message (for failed states)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateStateAsync(Guid executionId, ExecutionState state, string? errorMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates execution progress
    /// </summary>
    /// <param name="executionId">The execution ID</param>
    /// <param name="progressPercentage">Progress percentage (0-100)</param>
    /// <param name="statusMessage">Optional status message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateProgressAsync(Guid executionId, int progressPercentage, string? statusMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old executions (for cleanup)
    /// </summary>
    /// <param name="olderThan">Delete executions older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of executions deleted</returns>
    Task<int> DeleteOldExecutionsAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
