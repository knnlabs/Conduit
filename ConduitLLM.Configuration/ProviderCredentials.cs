namespace ConduitLLM.Configuration;

/// <summary>
/// Holds credentials and connection details for a specific LLM provider.
/// </summary>
public class ProviderCredentials
{
    /// <summary>
    /// Gets or sets the provider type enum value.
    /// This is used to link ModelProviderMapping to these credentials.
    /// </summary>
    public required ProviderType ProviderType { get; set; }

    /// <summary>
    /// Gets or sets the API key for the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API secret for providers that require it (e.g., AWS Secret Access Key).
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the provider's API endpoint.
    /// If null, a default endpoint might be used by the specific provider client.
    /// </summary>
    public string? BaseUrl { get; set; }


    // Consider adding a dictionary for provider-specific settings if needed later:
    // public Dictionary<string, string>? AdditionalSettings { get; set; }
}
