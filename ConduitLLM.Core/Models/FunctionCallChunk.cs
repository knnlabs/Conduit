using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a partial chunk of a function call in a streaming response.
/// </summary>
public class FunctionCallChunk
{
    /// <summary>
    /// The name of the function being called. May be null or partial during streaming.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>
    /// The arguments to the function as a JSON-encoded string. May be null or partial during streaming.
    /// </summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; set; }
}
