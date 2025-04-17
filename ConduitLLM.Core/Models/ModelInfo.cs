using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents information about a specific LLM model, formatted similarly to OpenAI's /models response.
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// The model identifier, which can be referenced in the API endpoints.
    /// Corresponds to the 'ModelAlias' used internally for routing configuration.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The object type, which is always "model".
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; } = "model";

    /// <summary>
    /// The organization that owns the model (e.g., "openai", "anthropic", "google").
    /// This might correspond to the 'ProviderId' used internally.
    /// </summary>
    [JsonPropertyName("owned_by")]
    public required string OwnedBy { get; set; }

    /// <summary>
    /// Maximum context window size in tokens for this model. (Optional)
    /// </summary>
    [JsonPropertyName("context_window")] // Example of aligning with potential standard names
    public int? MaxContextTokens { get; set; }

    // Note: Removed ProviderModelName and ProviderId as they are less standard for the /models endpoint.
    // 'OwnedBy' can represent the provider. 'Id' represents the usable model alias.
    // MaxOutputTokens is less common in the standard /models response but could be added if needed.
    // Other properties like 'created' (timestamp) or 'permission' could be added for closer OpenAI parity if available.
}
