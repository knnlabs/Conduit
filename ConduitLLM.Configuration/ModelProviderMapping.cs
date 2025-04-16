using System;

namespace ConduitLLM.Configuration;

/// <summary>
/// Defines a mapping between a user-friendly model alias and the specific provider and model ID to use.
/// </summary>
public class ModelProviderMapping
{
    /// <summary>
    /// Gets or sets the user-defined alias for the model (e.g., "gpt-4-turbo", "my-finetuned-model").
    /// This is the identifier the user will pass in the ChatCompletionRequest.Model property.
    /// </summary>
    public required string ModelAlias { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider configured in ProviderCredentials (e.g., "openai", "anthropic").
    /// This links the mapping to the correct set of credentials and connection details.
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the actual model ID that the target provider expects
    /// (e.g., "gpt-4-turbo-preview", "claude-3-opus-20240229").
    /// </summary>
    public required string ProviderModelId { get; set; }

    /// <summary>
    /// Gets or sets an optional deployment name for providers that support deployments 
    /// (e.g., Azure OpenAI deployments).
    /// </summary>
    public string? DeploymentName { get; set; }

    // Optional: Add fields for provider-specific overrides if needed,
    // such as a specific API key or base URL just for this mapping.
    // public string? ApiKeyOverride { get; set; }
    // public string? ApiBaseOverride { get; set; }
}
