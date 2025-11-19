using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Service for orchestrating agentic workflows with function calling.
/// Handles tool call execution, dependency detection, and cost aggregation.
/// </summary>
public interface IAgenticOrchestrationService
{
    /// <summary>
    /// Executes a list of tool calls from an LLM response.
    /// Detects dependencies and executes in parallel or sequentially as appropriate.
    /// </summary>
    /// <param name="toolCalls">List of tool calls from LLM response</param>
    /// <param name="virtualKeyId">Virtual key making the request</param>
    /// <param name="functionNameToIdMap">Mapping from function names to configuration IDs</param>
    /// <param name="requestId">Request correlation ID for audit trail</param>
    /// <param name="chatCompletionId">Parent chat completion ID for audit linking</param>
    /// <param name="iterationNumber">Current iteration number in the agentic loop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing executed tool calls, messages, and metrics</returns>
    Task<AgenticExecutionResult> ExecuteToolCallsAsync(
        List<ToolCall> toolCalls,
        int virtualKeyId,
        Dictionary<string, int> functionNameToIdMap,
        string requestId,
        Guid chatCompletionId,
        int iterationNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes tool calls to detect dependencies between them.
    /// Returns true if tool calls should be executed sequentially due to dependencies.
    /// </summary>
    /// <param name="toolCalls">List of tool calls to analyze</param>
    /// <returns>True if dependencies detected, false if safe to parallelize</returns>
    bool HasDependencies(List<ToolCall> toolCalls);
}

/// <summary>
/// Result of executing tool calls in an agentic workflow.
/// </summary>
public class AgenticExecutionResult
{
    /// <summary>
    /// List of messages to append to the conversation (tool result messages)
    /// </summary>
    public List<Message> ToolResultMessages { get; set; } = new();

    /// <summary>
    /// Summary of each function call that was executed
    /// </summary>
    public List<FunctionCallSummary> FunctionCallSummaries { get; set; } = new();

    /// <summary>
    /// Total cost of all function executions
    /// </summary>
    public decimal TotalFunctionCost { get; set; }

    /// <summary>
    /// Whether all function executions succeeded
    /// </summary>
    public bool AllSucceeded { get; set; }

    /// <summary>
    /// Any errors that occurred during execution
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
