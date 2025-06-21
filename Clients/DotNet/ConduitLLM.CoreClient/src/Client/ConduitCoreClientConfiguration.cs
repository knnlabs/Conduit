using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.CoreClient.Client;

/// <summary>
/// Configuration settings for the Conduit Core API client.
/// </summary>
public class ConduitCoreClientConfiguration
{
    /// <summary>
    /// Gets or sets the API key for authentication with the Core API.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the Core API.
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.conduit.example.com";

    /// <summary>
    /// Gets or sets the request timeout in seconds. Default is 60 seconds.
    /// </summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts. Default is 3.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the retry delay in milliseconds. Default is 1000ms.
    /// </summary>
    [Range(100, 10000)]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets additional headers to include with requests.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();

    /// <summary>
    /// Gets or sets the organization ID (optional).
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Creates a configuration instance from an API key.
    /// </summary>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="baseUrl">Optional base URL override.</param>
    /// <returns>A new configuration instance.</returns>
    public static ConduitCoreClientConfiguration FromApiKey(string apiKey, string? baseUrl = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        }

        return new ConduitCoreClientConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://api.conduit.example.com"
        };
    }

    /// <summary>
    /// Creates a configuration instance from environment variables.
    /// </summary>
    /// <returns>A new configuration instance populated from environment variables.</returns>
    public static ConduitCoreClientConfiguration FromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("CONDUIT_API_KEY");
        var baseUrl = Environment.GetEnvironmentVariable("CONDUIT_BASE_URL");
        var organizationId = Environment.GetEnvironmentVariable("CONDUIT_ORGANIZATION_ID");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("CONDUIT_API_KEY environment variable is required");
        }

        return new ConduitCoreClientConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://api.conduit.example.com",
            OrganizationId = organizationId
        };
    }
}