using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Models;

/// <summary>
/// Represents navigation state information with capability-based UI section availability.
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

    /// <summary>
    /// Gets or sets the capability-based UI section availability.
    /// </summary>
    public UISectionAvailability Sections { get; set; } = new();

    /// <summary>
    /// Gets or sets the provider details with health and model information.
    /// </summary>
    public List<ProviderNavigationInfo> ProviderDetails { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this state was last refreshed.
    /// </summary>
    public DateTime LastRefreshed { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets any error message if state loading failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents UI section availability based on model capabilities.
/// </summary>
public class UISectionAvailability
{
    /// <summary>
    /// Gets or sets whether chat functionality is available.
    /// </summary>
    public bool Chat { get; set; }

    /// <summary>
    /// Gets or sets whether embeddings functionality is available.
    /// </summary>
    public bool Embeddings { get; set; }

    /// <summary>
    /// Gets or sets whether image generation functionality is available.
    /// </summary>
    public bool Images { get; set; }

    /// <summary>
    /// Gets or sets whether video generation functionality is available.
    /// </summary>
    public bool Video { get; set; }

    /// <summary>
    /// Gets or sets whether audio functionality is available.
    /// </summary>
    public bool Audio { get; set; }

    /// <summary>
    /// Gets the total number of available capabilities.
    /// </summary>
    public int AvailableCount => 
        (Chat ? 1 : 0) + (Embeddings ? 1 : 0) + (Images ? 1 : 0) + (Video ? 1 : 0) + (Audio ? 1 : 0);
}

/// <summary>
/// Represents provider information for navigation display.
/// </summary>
public class ProviderNavigationInfo
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the provider health status.
    /// </summary>
    public ProviderHealthStatus HealthStatus { get; set; } = ProviderHealthStatus.Unknown;

    /// <summary>
    /// Gets or sets the health details if available.
    /// </summary>
    public string? HealthDetails { get; set; }

    /// <summary>
    /// Gets or sets the response time in milliseconds if available.
    /// </summary>
    public double? ResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the models available from this provider.
    /// </summary>
    public List<ModelNavigationInfo> Models { get; set; } = new();

    /// <summary>
    /// Gets the health status emoji for display.
    /// </summary>
    public string HealthEmoji => HealthStatus switch
    {
        ProviderHealthStatus.Healthy => "🟢",
        ProviderHealthStatus.Degraded => "🟡",
        ProviderHealthStatus.Unhealthy => "🔴",
        _ => "⚫"
    };

    /// <summary>
    /// Gets the health status text for display.
    /// </summary>
    public string HealthText => HealthStatus switch
    {
        ProviderHealthStatus.Healthy => "Healthy",
        ProviderHealthStatus.Degraded => "Degraded",
        ProviderHealthStatus.Unhealthy => "Unhealthy",
        _ => "Unknown"
    };
}

/// <summary>
/// Represents model information for navigation display.
/// </summary>
public class ModelNavigationInfo
{
    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model alias from mapping.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// Gets or sets the list of capabilities.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the model mapping is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether the model is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets the display name for the model.
    /// </summary>
    public string DisplayName => !string.IsNullOrEmpty(Alias) ? $"{Alias} ({ModelId})" : ModelId;

    /// <summary>
    /// Gets the capabilities as a formatted string for display.
    /// </summary>
    public string CapabilitiesText => string.Join(", ", Capabilities);
}

/// <summary>
/// Represents provider health status.
/// </summary>
public enum ProviderHealthStatus
{
    /// <summary>
    /// Health status is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// Provider is healthy and fully operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Provider is experiencing some issues but still functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Provider is unhealthy and not functioning properly.
    /// </summary>
    Unhealthy
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