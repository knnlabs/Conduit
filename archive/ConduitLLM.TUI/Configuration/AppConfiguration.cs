using ConduitLLM.TUI.Constants;

namespace ConduitLLM.TUI.Configuration;

public class AppConfiguration
{
    public string MasterKey { get; set; } = string.Empty;
    public string CoreApiUrl { get; set; } = UIConstants.Configuration.DefaultCoreApiUrl;
    public string AdminApiUrl { get; set; } = UIConstants.Configuration.DefaultAdminApiUrl;
    public string? SelectedVirtualKey { get; set; }
}