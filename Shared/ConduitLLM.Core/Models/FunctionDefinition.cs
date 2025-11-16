using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a function definition that the model can call.
/// </summary>
public class FunctionDefinition
{
    /// <summary>
    /// The name of the function to be called. Must be a-z, A-Z, 0-9, or contain underscores and dashes, 
    /// with a maximum length of 64 characters.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// A description of what the function does. This helps the model determine when and how to call the function.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// The parameters the functions accepts, described as a JSON Schema object.
    /// </summary>
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Parameters { get; set; }
}
