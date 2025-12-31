using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Options for streaming chat completions.
/// </summary>
/// <remarks>
/// Supported by OpenAI and OpenAI-compatible providers (Groq, SambaNova, Cerebras, etc.).
/// When include_usage is true, the provider will return token usage information in the
/// final chunk of the streaming response.
/// </remarks>
public class StreamOptions
{
    /// <summary>
    /// If set to true, the final chunk will include usage information (prompt_tokens, completion_tokens, total_tokens).
    /// This is essential for accurate billing and performance metrics in streaming mode.
    /// </summary>
    /// <remarks>
    /// Without this option, most providers do not return usage data in streaming responses,
    /// forcing the system to fall back to inaccurate token estimation methods.
    /// </remarks>
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
}
