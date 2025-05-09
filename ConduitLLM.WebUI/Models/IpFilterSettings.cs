namespace ConduitLLM.WebUI.Models;

/// <summary>
/// Represents the global settings for IP filtering
/// </summary>
public class IpFilterSettings
{
    /// <summary>
    /// Whether IP filtering is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Default filter mode when no specific rules match (true = allow, false = deny)
    /// </summary>
    public bool DefaultAllow { get; set; } = true;
    
    /// <summary>
    /// Whether to bypass filtering for admin UI access
    /// </summary>
    public bool BypassForAdminUi { get; set; } = true;
    
    /// <summary>
    /// List of endpoints to exclude from IP filtering
    /// </summary>
    public List<string> ExcludedEndpoints { get; set; } = new() { "/api/v1/health" };
}