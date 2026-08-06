using ConduitLLM.Functions.Entities;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Service interface for executing functions with reserve-then-commit billing.
/// </summary>
public interface IFunctionExecutionService
{
    /// <summary>
    /// Executes a function with reserve-then-commit billing.
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration to use.</param>
    /// <param name="virtualKeyId">The virtual key for billing.</param>
    /// <param name="parameters">Function-specific parameters.</param>
    /// <param name="idempotencyKey">Optional idempotency key to prevent duplicate executions.</param>
    /// <param name="metadata">Optional metadata for tracking.</param>
    /// <param name="providerToolName">
    /// For dynamic multi-tool providers (MCP), the provider-native tool name to invoke. Null for
    /// fixed-schema providers (Exa, Tavily), which map to a single implicit tool.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The function execution result with billing details.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid or balance is insufficient.</exception>
    /// <exception cref="ArgumentException">Thrown when required parameters are missing or invalid.</exception>
    Task<FunctionExecution> ExecuteAsync(
        int functionConfigurationId,
        int virtualKeyId,
        Dictionary<string, object> parameters,
        string? idempotencyKey = null,
        Dictionary<string, object>? metadata = null,
        string? providerToolName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the execution status by ID.
    /// </summary>
    /// <param name="executionId">The execution ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution entity, or null if not found.</returns>
    Task<FunctionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists executions for a virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of function executions.</returns>
    Task<List<FunctionExecution>> ListExecutionsAsync(
        int virtualKeyId,
        CancellationToken cancellationToken = default);
}
