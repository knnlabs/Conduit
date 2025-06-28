namespace ConduitLLM.TUI.Configuration;

public class AppConfiguration
{
    public string MasterKey { get; set; } = string.Empty;
    public string CoreApiUrl { get; set; } = "http://localhost:5000";
    public string AdminApiUrl { get; set; } = "http://localhost:5002";
    public string? SelectedVirtualKey { get; set; }
}