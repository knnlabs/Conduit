using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Response DTO for asynchronous function execution (202 Accepted)
/// </summary>
public class AsyncFunctionExecutionResponseDto
{
    /// <summary>
    /// Unique identifier for this execution
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Initial status (should be Pending)
    /// </summary>
    public ExecutionState Status { get; set; }

    /// <summary>
    /// URL to poll for status updates
    /// </summary>
    public required string StatusUrl { get; set; }

    /// <summary>
    /// Estimated cost for the execution
    /// </summary>
    public decimal? EstimatedCost { get; set; }
}
