namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Represents token usage information for a completion request.
/// </summary>
public class Usage
{
    /// <summary>
    /// Gets or sets the number of tokens in the prompt.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens in the completion.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Gets or sets the total number of tokens used.
    /// </summary>
    public int TotalTokens { get; set; }
}

/// <summary>
/// Represents the response format for completions.
/// </summary>
public class ResponseFormat
{
    /// <summary>
    /// Gets or sets the response format type.
    /// </summary>
    public string Type { get; set; } = "text";
}

/// <summary>
/// Represents a function call in a tool call.
/// </summary>
public class FunctionCall
{
    /// <summary>
    /// Gets or sets the name of the function to call.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments for the function call as a JSON string.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>
/// Represents a tool call in a message.
/// </summary>
public class ToolCall
{
    /// <summary>
    /// Gets or sets the unique identifier for the tool call.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of tool call.
    /// </summary>
    public string Type { get; set; } = "function";

    /// <summary>
    /// Gets or sets the function call details.
    /// </summary>
    public FunctionCall Function { get; set; } = new();
}

/// <summary>
/// Represents a function definition for tools.
/// </summary>
public class FunctionDefinition
{
    /// <summary>
    /// Gets or sets the name of the function.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what the function does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the parameters schema for the function.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Represents a tool that can be called by the model.
/// </summary>
public class Tool
{
    /// <summary>
    /// Gets or sets the type of tool.
    /// </summary>
    public string Type { get; set; } = "function";

    /// <summary>
    /// Gets or sets the function definition.
    /// </summary>
    public FunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// Represents the reason why a completion finished.
/// </summary>
public enum FinishReason
{
    /// <summary>
    /// The completion finished naturally.
    /// </summary>
    Stop,

    /// <summary>
    /// The completion was stopped due to length limit.
    /// </summary>
    Length,

    /// <summary>
    /// The completion finished because the model called a tool.
    /// </summary>
    ToolCalls,

    /// <summary>
    /// The completion was stopped by content filter.
    /// </summary>
    ContentFilter
}

/// <summary>
/// Represents performance metrics for a request.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the name of the provider that handled the request.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response time from the provider in milliseconds.
    /// </summary>
    public int ProviderResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the total response time including routing overhead in milliseconds.
    /// </summary>
    public int TotalResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the tokens generated per second.
    /// </summary>
    public double? TokensPerSecond { get; set; }
}