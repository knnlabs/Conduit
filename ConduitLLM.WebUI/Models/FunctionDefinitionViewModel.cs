using System.Text.Json.Nodes;

namespace ConduitLLM.WebUI.Models;

/// <summary>
/// View model for function definitions in the Chat playground.
/// </summary>
public class FunctionDefinitionViewModel
{
    /// <summary>
    /// Unique identifier for the function definition.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The name of the function.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A description of what the function does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON Schema defining the function parameters as a string.
    /// </summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>
    /// Whether this function is enabled for use.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Category of the function (e.g., "demo", "custom").
    /// </summary>
    public string Category { get; set; } = "custom";

    /// <summary>
    /// Gets the parameters as a JsonObject for validation.
    /// </summary>
    /// <returns>JsonObject or null if parsing fails.</returns>
    public JsonObject? GetParametersAsJson()
    {
        try
        {
            return JsonNode.Parse(ParametersJson)?.AsObject();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Represents the state of function calling in a chat session.
/// </summary>
public class FunctionCallState
{
    /// <summary>
    /// Pending function calls from the LLM.
    /// </summary>
    public List<ConduitLLM.Core.Models.ToolCall> PendingCalls { get; set; } = new();

    /// <summary>
    /// Results of executed functions.
    /// </summary>
    public Dictionary<string, FunctionExecutionResult> Results { get; set; } = new();

    /// <summary>
    /// Whether function calls are currently being processed.
    /// </summary>
    public bool IsProcessing { get; set; }
}

/// <summary>
/// Result of a function execution.
/// </summary>
public class FunctionExecutionResult
{
    /// <summary>
    /// Whether the function executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result data as a JSON string.
    /// </summary>
    public string Result { get; set; } = "{}";

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Time taken to execute the function.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}