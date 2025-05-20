namespace ConduitLLM.Configuration.DTOs.IpFilter;

/// <summary>
/// DTO for IP filter settings
/// </summary>
public class IpFilterSettingsDto
{
    /// <summary>
    /// Whether IP filtering is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether to allow by default (true) or deny by default (false)
    /// </summary>
    public bool DefaultAllow { get; set; }

    /// <summary>
    /// Whether to bypass filtering for the admin UI
    /// </summary>
    public bool BypassForAdminUi { get; set; }

    /// <summary>
    /// List of endpoint paths to exclude from filtering
    /// </summary>
    public List<string> ExcludedEndpoints { get; set; } = new();

    /// <summary>
    /// The filter mode: "permissive" (default allow) or "restrictive" (default deny)
    /// </summary>
    public string FilterMode { get; set; } = "permissive";

    /// <summary>
    /// List of whitelist filters
    /// </summary>
    public List<IpFilterDto> WhitelistFilters { get; set; } = new List<IpFilterDto>();

    /// <summary>
    /// List of blacklist filters
    /// </summary>
    public List<IpFilterDto> BlacklistFilters { get; set; } = new List<IpFilterDto>();
}