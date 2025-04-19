using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a tool that can be called by the LLM during a chat completion.
/// </summary>
public class Tool
{
    /// <summary>
    /// The type of tool. Currently only "function" is standard across providers.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// The function definition for this tool.
    /// </summary>
    [JsonPropertyName("function")]
    public required ConduitLLM.Core.Models.FunctionDefinition Function { get; set; }
}
