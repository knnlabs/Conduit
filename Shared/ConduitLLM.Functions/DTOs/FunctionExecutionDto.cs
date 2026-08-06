using ConduitLLM.Functions.Enums;
using System.Text.Json;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Canonical function execution resource shared by Gateway and Admin APIs.
/// </summary>
public class FunctionExecutionDto
{
    /// <summary>
    /// Unique identifier for this execution
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Function configuration that was executed.
    /// </summary>
    public int FunctionId { get; set; }

    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public ExecutionState Status { get; set; }

    /// <summary>
    /// Structured input supplied to the function.
    /// </summary>
    public Dictionary<string, JsonElement>? Input { get; set; }

    /// <summary>
    /// Structured output returned by the function.
    /// </summary>
    public Dictionary<string, JsonElement>? Output { get; set; }

    /// <summary>
    /// Error message when execution did not complete successfully.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// When the execution was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Execution duration in integer milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Structured execution cost.
    /// </summary>
    public FunctionExecutionCostDto Cost { get; set; } = new();
}

/// <summary>
/// Monetary details for a function execution.
/// </summary>
public class FunctionExecutionCostDto
{
    /// <summary>
    /// Cost estimated before execution.
    /// </summary>
    public decimal? Estimated { get; set; }

    /// <summary>
    /// Final cost after execution.
    /// </summary>
    public decimal? Actual { get; set; }

    /// <summary>
    /// ISO 4217 currency code.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Provider-specific structured cost calculation details.
    /// </summary>
    public Dictionary<string, JsonElement>? Breakdown { get; set; }
}
