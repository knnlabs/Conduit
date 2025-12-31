using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Response DTO for synchronous function execution
/// </summary>
public class FunctionExecutionResponseDto
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
    /// Result data from the function (null if not completed)
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Cost of the execution in USD
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
