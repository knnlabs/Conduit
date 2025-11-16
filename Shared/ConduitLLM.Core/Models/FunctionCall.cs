using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a function call made by the assistant in a chat completion response.
/// </summary>
public class FunctionCall
{
    /// <summary>
    /// The name of the function being called.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// The arguments to the function as a JSON-encoded string. 
    /// These arguments match the function's parameters as defined in the tool definition.
    /// </summary>
    [JsonPropertyName("arguments")]
    public required string Arguments { get; set; }
}
