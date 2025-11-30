using System.Text.Json;
using ConduitLLM.Gateway.Controllers;

namespace ConduitLLM.Gateway.Utilities;

/// <summary>
/// Utility class for serializing function execution results for request logging metadata.
/// </summary>
public static class FunctionExecutionSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new() { WriteIndented = false };

    /// <summary>
    /// Serializes function execution results to JSON for request log metadata.
    /// This provides richer data than the basic chat tool calls format,
    /// including execution status, cost, and links to FunctionExecution records.
    /// </summary>
    /// <param name="results">The list of function execution results to serialize.</param>
    /// <returns>JSON string representation of the function execution results.</returns>
    public static string SerializeFunctionExecutionResults(List<FunctionExecutionResultForLogging> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return JsonSerializer.Serialize(new
        {
            type = "chat_with_functions",
            functionCallCount = results.Count,
            totalCost = results.Sum(r => r.Cost ?? 0m),
            successCount = results.Count(r => r.Status == "completed"),
            failedCount = results.Count(r => r.Status == "failed"),
            functionCalls = results.Select(r => new
            {
                toolCallId = r.ToolCallId,
                functionName = r.FunctionName,
                status = r.Status,
                cost = r.Cost,
                errorMessage = r.ErrorMessage,
                functionExecutionId = r.FunctionExecutionId
            })
        }, DefaultOptions);
    }

    /// <summary>
    /// Deserializes function execution metadata JSON back to a structured object.
    /// Useful for parsing stored metadata from request logs.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>Deserialized function execution metadata or null if parsing fails.</returns>
    public static FunctionExecutionMetadata? DeserializeFunctionExecutionMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<FunctionExecutionMetadata>(json, DefaultOptions);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Represents the metadata structure for function execution results stored in request logs.
/// </summary>
public class FunctionExecutionMetadata
{
    /// <summary>
    /// The type identifier for this metadata (always "chat_with_functions").
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Total number of function calls in this request.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("functionCallCount")]
    public int FunctionCallCount { get; set; }

    /// <summary>
    /// Total cost of all function executions.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Number of successful function executions.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("successCount")]
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed function executions.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("failedCount")]
    public int FailedCount { get; set; }

    /// <summary>
    /// List of individual function call details.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("functionCalls")]
    public List<FunctionCallMetadata>? FunctionCalls { get; set; }
}

/// <summary>
/// Represents metadata for a single function call.
/// </summary>
public class FunctionCallMetadata
{
    /// <summary>
    /// The tool call ID from the LLM response.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Name of the function that was executed.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("functionName")]
    public string? FunctionName { get; set; }

    /// <summary>
    /// Execution status: "completed" or "failed".
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Cost of the function execution.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("cost")]
    public decimal? Cost { get; set; }

    /// <summary>
    /// Error message if the function failed.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// ID of the FunctionExecution record for audit/drill-down.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("functionExecutionId")]
    public Guid? FunctionExecutionId { get; set; }
}
