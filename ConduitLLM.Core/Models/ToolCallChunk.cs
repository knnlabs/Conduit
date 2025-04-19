using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a partial chunk of a tool call in a streaming response.
/// </summary>
public class ToolCallChunk
{
    /// <summary>
    /// The index of this tool call in the list of tool calls.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// The ID of the tool call. May be null or partial during streaming.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>
    /// The type of tool call. Currently only "function" is standard.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    /// <summary>
    /// The function call details if this is a function tool call. May be null or partial during streaming.
    /// </summary>
    [JsonPropertyName("function")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ConduitLLM.Core.Models.FunctionCallChunk? Function { get; set; }
}
