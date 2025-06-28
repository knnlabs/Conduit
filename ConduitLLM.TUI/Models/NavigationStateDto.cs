using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Models;

/// <summary>
/// Represents navigation state information (placeholder until SDK support).
/// </summary>
public class NavigationStateDto
{
    /// <summary>
    /// Gets or sets the list of available models.
    /// </summary>
    public List<string> Models { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of available providers.
    /// </summary>
    public List<string> Providers { get; set; } = new();
}

/// <summary>
/// Represents a navigation state update event.
/// </summary>
public class NavigationStateUpdateDto
{
    /// <summary>
    /// Gets or sets the list of model mappings.
    /// </summary>
    public List<ModelProviderMappingDto> ModelMappings { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of provider health statuses.
    /// </summary>
    public List<ProviderHealthStatusDto> ProviderHealthStatuses { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the update.
    /// </summary>
    public DateTime Timestamp { get; set; }
}