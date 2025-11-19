using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Custom streaming chunk sent during function execution in agentic workflows.
/// Allows clients to track function execution status in real-time during streaming.
/// </summary>
public class FunctionExecutionStatusChunk : ChatCompletionChunk
{
    /// <summary>
    /// Discriminator field to identify this as a function execution status chunk.
    /// Value is always "function_execution" for this chunk type.
    /// </summary>
    [JsonPropertyName("chunk_type")]
    public string ChunkType { get; set; } = "function_execution";

    /// <summary>
    /// ID of the tool call this status update refers to
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Name of the function being executed
    /// </summary>
    [JsonPropertyName("function_name")]
    public string? FunctionName { get; set; }

    /// <summary>
    /// Current status of the function execution
    /// </summary>
    /// <remarks>
    /// Possible values:
    /// - "started": Function execution has begun
    /// - "completed": Function execution finished successfully
    /// - "failed": Function execution failed with an error
    /// </remarks>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Function execution result (present when Status = "completed")
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// Cost of the function execution (present when Status = "completed" or "failed")
    /// </summary>
    [JsonPropertyName("cost")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Cost { get; set; }

    /// <summary>
    /// Error message (present when Status = "failed")
    /// </summary>
    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Function execution ID for audit trail lookup
    /// </summary>
    [JsonPropertyName("function_execution_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? FunctionExecutionId { get; set; }
}

/// <summary>
/// Status constants for function execution
/// </summary>
public static class FunctionExecutionStatus
{
    public const string Started = "started";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
