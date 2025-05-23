namespace ConduitLLM.Configuration.Options;

/// <summary>
/// Configuration options for IP filtering
/// </summary>
public class IpFilterOptions
{
    /// <summary>
    /// Section name in configuration
    /// </summary>
    public const string SectionName = "IpFilter";
    
    /// <summary>
    /// Whether IP filtering is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Default filter mode when no specific rules match (true = allow, false = deny)
    /// </summary>
    public bool DefaultAllow { get; set; } = true;
    
    /// <summary>
    /// Whether to enable IPv6 filtering support
    /// </summary>
    public bool EnableIpv6 { get; set; } = true;
    
    /// <summary>
    /// Whether to bypass filtering for admin UI access (leave true for safety)
    /// </summary>
    public bool BypassForAdminUi { get; set; } = true;
    
    /// <summary>
    /// List of endpoints to exclude from IP filtering (e.g., health checks)
    /// </summary>
    public List<string> ExcludedEndpoints { get; set; } = new() { "/api/v1/health" };
}