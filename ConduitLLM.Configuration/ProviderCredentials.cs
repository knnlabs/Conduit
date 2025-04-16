using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration;

/// <summary>
/// Holds credentials and connection details for a specific LLM provider.
/// </summary>
public class ProviderCredentials
{
    /// <summary>
    /// Gets or sets the unique name identifying the provider (e.g., "openai", "anthropic", "azure").
    /// This name is used to link ModelProviderMapping to these credentials.
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the API key for the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the provider's API endpoint.
    /// If null, a default endpoint might be used by the specific provider client.
    /// </summary>
    public string? ApiBase { get; set; }

    /// <summary>
    /// Gets or sets the API version, particularly relevant for providers like Azure OpenAI.
    /// </summary>
    public string? ApiVersion { get; set; }

    // Consider adding a dictionary for provider-specific settings if needed later:
    // public Dictionary<string, string>? AdditionalSettings { get; set; }
}
