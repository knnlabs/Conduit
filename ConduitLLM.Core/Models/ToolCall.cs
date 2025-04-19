using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a tool call made by the assistant in a chat completion response.
/// </summary>
public class ToolCall
{
    /// <summary>
    /// The ID of the tool call. This ID is used to match tool responses to tool calls.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The type of tool call. Currently only "function" is standard.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// The function call details if this is a function tool call.
    /// </summary>
    [JsonPropertyName("function")]
    public required ConduitLLM.Core.Models.FunctionCall Function { get; set; }
}
