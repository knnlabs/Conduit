using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Providers.Configuration;

/// <summary>
/// Configuration options for HTTP request timeouts in LLM provider clients.
/// </summary>
public class TimeoutOptions
{
    /// <summary>
    /// The configuration section name for timeout settings
    /// </summary>
    public const string SectionName = "HttpTimeout";
    
    /// <summary>
    /// Default timeout duration in seconds for HTTP requests (default: 100 seconds)
    /// </summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 100;
    
    /// <summary>
    /// Whether to enable timeout logging (default: true)
    /// </summary>
    public bool EnableTimeoutLogging { get; set; } = true;
}
