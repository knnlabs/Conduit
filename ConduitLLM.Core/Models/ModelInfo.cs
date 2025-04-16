namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents information about a specific LLM model configured within the system.
/// This might be loaded from configuration.
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// The alias or identifier used within this library to refer to the model.
    /// </summary>
    public required string ModelAlias { get; set; }

    /// <summary>
    /// The actual model name used by the underlying provider (e.g., "gpt-4", "claude-3-opus-20240229").
    /// </summary>
    public required string ProviderModelName { get; set; }

    /// <summary>
    /// The identifier of the provider handling this model (e.g., "openai", "anthropic", "azure").
    /// </summary>
    public required string ProviderId { get; set; }

    /// <summary>
    /// Maximum context window size in tokens for this model. (Optional)
    /// </summary>
    public int? MaxContextTokens { get; set; }

    /// <summary>
    /// Maximum number of output tokens this model can generate in a single response. (Optional)
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    // Add other relevant model properties as needed (e.g., cost per token, supported features)
}
