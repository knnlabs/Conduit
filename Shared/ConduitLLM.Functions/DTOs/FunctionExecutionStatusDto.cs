using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for checking function execution status (used for polling)
/// </summary>
public class FunctionExecutionStatusDto
{
    /// <summary>
    /// Unique identifier for this execution
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Current status of the execution
    /// </summary>
    public ExecutionState Status { get; set; }

    /// <summary>
    /// Progress percentage (0-100) for long-running executions
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// Optional status message
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// When the execution was requested
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the execution started (null if not started yet)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the execution completed (null if not completed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Result data (only populated when status is Completed)
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Estimated cost (populated before execution)
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Actual cost (populated after execution)
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Execution duration in seconds
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
