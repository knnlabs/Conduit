using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Client;

/// <summary>
/// Configuration settings for the Conduit Admin API client.
/// </summary>
public class ConduitAdminClientConfiguration
{
    /// <summary>
    /// Gets or sets the master key for authentication with the Admin API.
    /// </summary>
    [Required]
    public string MasterKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the Admin API.
    /// </summary>
    [Required]
    [Url]
    public string AdminApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the Core API (optional).
    /// </summary>
    [Url]
    public string? CoreApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the request timeout in seconds. Default is 30 seconds.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

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
    /// Gets or sets whether to enable caching. Default is true.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache timeout in seconds. Default is 300 seconds (5 minutes).
    /// </summary>
    [Range(1, 3600)]
    public int CacheTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Creates a configuration instance from environment variables.
    /// </summary>
    /// <returns>A new configuration instance populated from environment variables.</returns>
    public static ConduitAdminClientConfiguration FromEnvironment()
    {
        var masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY");
        var adminApiUrl = Environment.GetEnvironmentVariable("CONDUIT_ADMIN_API_URL") 
                         ?? Environment.GetEnvironmentVariable("CONDUIT_ADMIN_API_BASE_URL");
        var conduitApiUrl = Environment.GetEnvironmentVariable("CONDUIT_API_URL");

        if (string.IsNullOrEmpty(masterKey))
        {
            throw new InvalidOperationException("CONDUIT_API_TO_API_BACKEND_AUTH_KEY environment variable is required");
        }

        if (string.IsNullOrEmpty(adminApiUrl))
        {
            throw new InvalidOperationException("Either CONDUIT_ADMIN_API_URL or CONDUIT_ADMIN_API_BASE_URL environment variable is required");
        }

        return new ConduitAdminClientConfiguration
        {
            MasterKey = masterKey,
            AdminApiUrl = adminApiUrl,
            CoreApiUrl = conduitApiUrl
        };
    }

    /// <summary>
    /// Normalizes the API URL by removing trailing slashes and adding /api suffix if not present.
    /// </summary>
    /// <param name="url">The URL to normalize.</param>
    /// <returns>The normalized URL.</returns>
    public static string NormalizeApiUrl(string url)
    {
        var normalized = url.TrimEnd('/');
        
        if (!normalized.EndsWith("/api"))
        {
            normalized += "/api";
        }
        
        return normalized;
    }
}