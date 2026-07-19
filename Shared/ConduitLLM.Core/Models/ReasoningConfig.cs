using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Unified reasoning configuration (OpenRouter-style), passed through to providers that support it.
/// Only the fields that are set are serialized, so providers that don't support reasoning are
/// unaffected unless the caller explicitly requests it.
/// </summary>
public class ReasoningConfig
{
    /// <summary>
    /// Reasoning effort level: <c>max</c>, <c>xhigh</c>, <c>high</c>, <c>medium</c>, <c>low</c>,
    /// <c>minimal</c>, or <c>none</c> (OpenAI-style models). Maps to thinking levels on some providers.
    /// </summary>
    [JsonPropertyName("effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Effort { get; set; }

    /// <summary>
    /// Explicit reasoning-token budget (Anthropic/Gemini-style models).
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Enables reasoning with the provider's default parameters.
    /// </summary>
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; set; }

    /// <summary>
    /// When true, reasoning is used internally but excluded from the response.
    /// </summary>
    [JsonPropertyName("exclude")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Exclude { get; set; }
}
