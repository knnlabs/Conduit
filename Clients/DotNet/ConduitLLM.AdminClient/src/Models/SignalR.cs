using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// SignalR hub endpoints for Admin API.
/// </summary>
public static class SignalREndpoints
{
    /// <summary>
    /// Navigation state hub for model discovery and provider health updates.
    /// </summary>
    public const string NavigationStateHub = "/hubs/navigation-state";
    
    /// <summary>
    /// Admin notifications hub for virtual key events and configuration changes.
    /// </summary>
    public const string AdminNotificationsHub = "/hubs/admin-notifications";
}

#region Navigation State Events

/// <summary>
/// Navigation state update event triggered when entities change.
/// </summary>
public class NavigationStateUpdateEvent
{
    /// <summary>
    /// Gets or sets the timestamp of the update.
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets which entities have changed.
    /// </summary>
    [Required]
    public ChangedEntities ChangedEntities { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the summary of current state.
    /// </summary>
    [Required]
    public NavigationSummary Summary { get; set; } = new();
}

/// <summary>
/// Indicates which entities have changed.
/// </summary>
public class ChangedEntities
{
    /// <summary>
    /// Gets or sets whether model mappings have changed.
    /// </summary>
    public bool? ModelMappings { get; set; }
    
    /// <summary>
    /// Gets or sets whether providers have changed.
    /// </summary>
    public bool? Providers { get; set; }
    
    /// <summary>
    /// Gets or sets whether virtual keys have changed.
    /// </summary>
    public bool? VirtualKeys { get; set; }
    
    /// <summary>
    /// Gets or sets whether settings have changed.
    /// </summary>
    public bool? Settings { get; set; }
}

/// <summary>
/// Summary of navigation state.
/// </summary>
public class NavigationSummary
{
    /// <summary>
    /// Gets or sets the total number of providers.
    /// </summary>
    public int TotalProviders { get; set; }
    
    /// <summary>
    /// Gets or sets the number of enabled providers.
    /// </summary>
    public int EnabledProviders { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of model mappings.
    /// </summary>
    public int TotalMappings { get; set; }
    
    /// <summary>
    /// Gets or sets the number of active model mappings.
    /// </summary>
    public int ActiveMappings { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of virtual keys.
    /// </summary>
    public int TotalVirtualKeys { get; set; }
    
    /// <summary>
    /// Gets or sets the number of active virtual keys.
    /// </summary>
    public int ActiveVirtualKeys { get; set; }
}

/// <summary>
/// Model discovered event when a new model is found.
/// </summary>
public class ModelDiscoveredEvent
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public int ProviderId { get; set; }
    
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the discovered model details.
    /// </summary>
    [Required]
    public SignalRDiscoveredModel Model { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the timestamp of discovery.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Details of a discovered model in SignalR events.
/// </summary>
public class SignalRDiscoveredModel
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the model capabilities.
    /// </summary>
    [Required]
    public List<string> Capabilities { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the context window size.
    /// </summary>
    public int? ContextWindow { get; set; }
    
    /// <summary>
    /// Gets or sets the maximum output tokens.
    /// </summary>
    public int? MaxOutput { get; set; }
}

/// <summary>
/// Provider health change event.
/// </summary>
public class ProviderHealthChangeEvent
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public int ProviderId { get; set; }
    
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the previous health status.
    /// </summary>
    [Required]
    public string PreviousStatus { get; set; } = "unknown";
    
    /// <summary>
    /// Gets or sets the current health status.
    /// </summary>
    [Required]
    public string CurrentStatus { get; set; } = "unknown";
    
    /// <summary>
    /// Gets or sets the health score (0-100).
    /// </summary>
    public double HealthScore { get; set; }
    
    /// <summary>
    /// Gets or sets any health issues.
    /// </summary>
    public List<string>? Issues { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp of the health change.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region Admin Notification Events

/// <summary>
/// Virtual key event types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VirtualKeyEventType
{
    /// <summary>
    /// Virtual key was created.
    /// </summary>
    Created,
    
    /// <summary>
    /// Virtual key was updated.
    /// </summary>
    Updated,
    
    /// <summary>
    /// Virtual key was deleted.
    /// </summary>
    Deleted,
    
    /// <summary>
    /// Virtual key was enabled.
    /// </summary>
    Enabled,
    
    /// <summary>
    /// Virtual key was disabled.
    /// </summary>
    Disabled,
    
    /// <summary>
    /// Virtual key spend was updated.
    /// </summary>
    SpendUpdated
}

/// <summary>
/// Virtual key event notification.
/// </summary>
public class VirtualKeyEvent
{
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    [Required]
    public VirtualKeyEventType EventType { get; set; }
    
    /// <summary>
    /// Gets or sets the virtual key ID.
    /// </summary>
    public int VirtualKeyId { get; set; }
    
    /// <summary>
    /// Gets or sets the virtual key hash.
    /// </summary>
    [Required]
    public string VirtualKeyHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the virtual key name.
    /// </summary>
    public string? VirtualKeyName { get; set; }
    
    /// <summary>
    /// Gets or sets the list of changes.
    /// </summary>
    public List<FieldChange>? Changes { get; set; }
    
    /// <summary>
    /// Gets or sets event metadata.
    /// </summary>
    public VirtualKeyEventMetadata? Metadata { get; set; }
    
    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Field change details.
/// </summary>
public class FieldChange
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    [Required]
    public string Field { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the old value.
    /// </summary>
    public object? OldValue { get; set; }
    
    /// <summary>
    /// Gets or sets the new value.
    /// </summary>
    public object? NewValue { get; set; }
}

/// <summary>
/// Virtual key event metadata.
/// </summary>
public class VirtualKeyEventMetadata
{
    /// <summary>
    /// Gets or sets the current spend amount.
    /// </summary>
    public decimal? CurrentSpend { get; set; }
    
    /// <summary>
    /// Gets or sets the spend limit.
    /// </summary>
    public decimal? SpendLimit { get; set; }
    
    /// <summary>
    /// Gets or sets whether the key is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Configuration change event.
/// </summary>
public class ConfigurationChangeEvent
{
    /// <summary>
    /// Gets or sets the configuration category.
    /// </summary>
    [Required]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the setting name.
    /// </summary>
    [Required]
    public string Setting { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the old value.
    /// </summary>
    public object? OldValue { get; set; }
    
    /// <summary>
    /// Gets or sets the new value.
    /// </summary>
    public object? NewValue { get; set; }
    
    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string? ChangedBy { get; set; }
    
    /// <summary>
    /// Gets or sets the change timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Admin notification event.
/// </summary>
public class AdminNotificationEvent
{
    /// <summary>
    /// Gets or sets the notification ID.
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the notification type.
    /// </summary>
    [Required]
    public string Type { get; set; } = "info";
    
    /// <summary>
    /// Gets or sets the notification title.
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the notification message.
    /// </summary>
    [Required]
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets additional details.
    /// </summary>
    public object? Details { get; set; }
    
    /// <summary>
    /// Gets or sets whether action is required.
    /// </summary>
    public bool? ActionRequired { get; set; }
    
    /// <summary>
    /// Gets or sets available actions.
    /// </summary>
    public List<NotificationAction>? Actions { get; set; }
    
    /// <summary>
    /// Gets or sets the notification timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Notification action.
/// </summary>
public class NotificationAction
{
    /// <summary>
    /// Gets or sets the action label.
    /// </summary>
    [Required]
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the action identifier.
    /// </summary>
    [Required]
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets action-specific data.
    /// </summary>
    public object? Data { get; set; }
}

#endregion