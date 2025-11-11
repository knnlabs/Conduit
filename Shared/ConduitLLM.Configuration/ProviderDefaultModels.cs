namespace ConduitLLM.Configuration;

/// <summary>
/// Defines default model configurations for different providers and operation types.
/// This replaces hardcoded model defaults throughout the codebase.
/// </summary>
public class ProviderDefaultModels
{

    /// <summary>
    /// Gets or sets provider-specific default models.
    /// </summary>
    public Dictionary<string, ProviderSpecificDefaults> ProviderDefaults { get; set; } = new();
}


/// <summary>
/// Provider-specific default model configurations.
/// </summary>
public class ProviderSpecificDefaults
{
    /// <summary>
    /// Gets or sets the default model for chat completions.
    /// </summary>
    public string? DefaultChatModel { get; set; }

    /// <summary>
    /// Gets or sets the default model for embeddings.
    /// </summary>
    public string? DefaultEmbeddingModel { get; set; }

    /// <summary>
    /// Gets or sets the default model for image generation.
    /// </summary>
    public string? DefaultImageModel { get; set; }

    /// <summary>
    /// Gets or sets model aliases for this provider.
    /// Maps user-friendly names to provider-specific model IDs.
    /// </summary>
    public Dictionary<string, string> ModelAliases { get; set; } = new();
}
