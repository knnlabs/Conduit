using ConduitLLM.Functions.Enums;
using System.Text.Json;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for function execution data (Admin API)
/// </summary>
public class FunctionExecutionDto
{
    /// <summary>
    /// Unique identifier for this execution
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the function configuration being executed
    /// </summary>
    public int FunctionConfigurationId { get; set; }

    /// <summary>
    /// Foreign key to the virtual key used for authorization
    /// </summary>
    public int VirtualKeyId { get; set; }

    /// <summary>
    /// Execution mode for this specific execution (Synchronous or Asynchronous)
    /// </summary>
    public ExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// Current state of the execution
    /// </summary>
    public ExecutionState State { get; set; }

    /// <summary>
    /// When the execution was requested/created
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the execution actually started processing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the execution completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Execution duration in milliseconds (null if not completed)
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Input parameters for the function
    /// </summary>
    public JsonElement? Request { get; set; }

    /// <summary>
    /// Result data from the function
    /// </summary>
    public JsonElement? Response { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Estimated cost before execution (for balance reservation)
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Actual cost after execution (for final billing)
    /// </summary>
    public decimal? ActualCost { get; set; }

    /// <summary>
    /// Detailed cost calculation breakdown
    /// </summary>
    public JsonElement? CostCalculation { get; set; }

    /// <summary>
    /// Number of retry attempts made for this execution
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// When the next retry should be attempted (for failed executions)
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Optional webhook URL to notify when execution completes
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Whether the webhook has been successfully delivered
    /// </summary>
    public bool WebhookDelivered { get; set; }

    /// <summary>
    /// Progress percentage (0-100) for long-running executions
    /// </summary>
    public int? ProgressPercentage { get; set; }

    /// <summary>
    /// Optional status message for progress updates
    /// </summary>
    public string? StatusMessage { get; set; }
}
