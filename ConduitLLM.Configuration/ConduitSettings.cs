namespace ConduitLLM.Configuration;

/// <summary>
/// Represents the overall configuration settings for ConduitLLM.
/// This class is typically bound from configuration sources like appsettings.json.
/// </summary>
public class ConduitSettings
{
    /// <summary>
    /// Gets or sets the list of model mappings, defining how model aliases route to providers.
    /// </summary>
    public List<ModelProviderMapping> ModelMappings { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of provider credentials and connection details.
    /// </summary>
    public List<ProviderCredentials> ProviderCredentials { get; set; } = new();

    /// <summary>
    /// Gets or sets the default timeout in seconds for LLM requests.
    /// Provider-specific clients might override this.
    /// </summary>
    public int? DefaultTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the default number of retries for failed LLM requests.
    /// Provider-specific clients might override this or implement more sophisticated retry logic.
    /// </summary>
    public int? DefaultRetries { get; set; }

    // Add other global settings as needed, e.g., logging configuration, global API key (if applicable).
}
