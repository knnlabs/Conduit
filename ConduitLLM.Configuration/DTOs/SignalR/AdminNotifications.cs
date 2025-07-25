using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Navigation item state for admin UI.
    /// </summary>
    public class NavigationItemState
    {
        /// <summary>
        /// Gets or sets the navigation item ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the item is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the badge text.
        /// </summary>
        public string? BadgeText { get; set; }

        /// <summary>
        /// Gets or sets the badge color.
        /// </summary>
        public string? BadgeColor { get; set; }

        /// <summary>
        /// Gets or sets the tooltip text.
        /// </summary>
        public string? Tooltip { get; set; }

        /// <summary>
        /// Gets or sets additional metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Event args for navigation state changes.
    /// </summary>
    public class NavigationStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the navigation item that changed.
        /// </summary>
        public NavigationItemState Item { get; set; } = new();

        /// <summary>
        /// Gets or sets the type of change.
        /// </summary>
        public string ChangeType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for the change.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Configuration change notification.
    /// </summary>
    public class ConfigurationChangeNotification
    {
        /// <summary>
        /// Gets or sets the configuration type that changed.
        /// </summary>
        public string ConfigurationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specific configuration key.
        /// </summary>
        public string? ConfigurationKey { get; set; }

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
        /// Gets or sets when the change was made.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether this change requires a restart.
        /// </summary>
        public bool RequiresRestart { get; set; }

        /// <summary>
        /// Gets or sets the affected services.
        /// </summary>
        public List<string> AffectedServices { get; set; } = new();
    }

    /// <summary>
    /// Virtual key activity notification.
    /// </summary>
    public class VirtualKeyActivityNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Gets or sets the virtual key name.
        /// </summary>
        public string VirtualKeyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the activity type.
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the activity description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request count in the last period.
        /// </summary>
        public int? RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the spend amount in the last period.
        /// </summary>
        public decimal? SpendAmount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the activity.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets additional context.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Provider configuration update notification.
    /// </summary>
    public class ProviderConfigurationNotification
    {
        /// <summary>
        /// Gets or sets the provider type.
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the configuration action.
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the provider is now enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the models affected.
        /// </summary>
        public List<string> AffectedModels { get; set; } = new();

        /// <summary>
        /// Gets or sets who made the change.
        /// </summary>
        public string? ChangedBy { get; set; }

        /// <summary>
        /// Gets or sets when the change was made.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets validation results if any.
        /// </summary>
        public Dictionary<string, string> ValidationResults { get; set; } = new();
    }

    /// <summary>
    /// Model mapping change notification.
    /// </summary>
    public class ModelMappingChangeNotification
    {
        /// <summary>
        /// Gets or sets the mapping ID.
        /// </summary>
        public int MappingId { get; set; }

        /// <summary>
        /// Gets or sets the model alias.
        /// </summary>
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider type.
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the actual model name.
        /// </summary>
        public string ActualModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the change type.
        /// </summary>
        public string ChangeType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets who made the change.
        /// </summary>
        public string? ChangedBy { get; set; }

        /// <summary>
        /// Gets or sets when the change was made.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the previous mapping if this was an update.
        /// </summary>
        public string? PreviousMapping { get; set; }
    }

    /// <summary>
    /// Virtual key notification for administrative updates.
    /// </summary>
    public class VirtualKeyNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "VirtualKeyUpdate";

        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Gets or sets the update type (created, updated, deleted).
        /// </summary>
        public string UpdateType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional details about the update.
        /// </summary>
        public object? Details { get; set; }
    }

    /// <summary>
    /// Security alert notification for administrators.
    /// </summary>
    public class SecurityAlertNotification : SystemNotification
    {
        /// <summary>
        /// Gets the notification type.
        /// </summary>
        public override string Type => "SecurityAlert";

        /// <summary>
        /// Gets or sets the alert type.
        /// </summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alert description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the affected resource.
        /// </summary>
        public string? AffectedResource { get; set; }
    }
}