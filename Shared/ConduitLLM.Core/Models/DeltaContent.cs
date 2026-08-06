using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents the delta content within a streaming choice.
/// </summary>
public class DeltaContent
{
    /// <summary>
    /// The role of the author of this message.
    /// </summary>
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    /// <summary>
    /// The contents of the message.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    /// <summary>
    /// The tool calls made by the assistant in this delta chunk.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCallChunk>? ToolCalls { get; set; }
    
    /// <summary>
    /// Reasoning content for models that support reasoning (e.g., gpt-oss-120b on Groq).
    /// </summary>
    [JsonPropertyName("reasoning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reasoning { get; set; }
    
    /// <summary>
    /// Channel indicator for reasoning models (e.g., "analysis" for reasoning chunks).
    /// </summary>
    [JsonPropertyName("channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Channel { get; set; }

    /// <summary>Incremental assistant audio output.</summary>
    [JsonPropertyName("audio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Audio { get; set; }

    /// <summary>Incremental assistant image output.</summary>
    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Images { get; set; }

    /// <summary>Structured provider reasoning details.</summary>
    [JsonPropertyName("reasoning_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ReasoningDetails { get; set; }
    
    /// <summary>
    /// Extension data to capture any additional fields not explicitly mapped.
    /// This allows us to be a true proxy and pass through provider-specific fields.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
