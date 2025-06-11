namespace ConduitLLM.WebUI.Models;

/// <summary>
/// Types of notifications in the system
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// General system information
    /// </summary>
    System,

    /// <summary>
    /// Virtual key validation failures
    /// </summary>
    VirtualKeyValidation,

    /// <summary>
    /// Warnings about budget limits being approached
    /// </summary>
    BudgetWarning,

    /// <summary>
    /// Key expiration notifications
    /// </summary>
    KeyExpiration,

    /// <summary>
    /// Security related alerts
    /// </summary>
    Security,

    /// <summary>
    /// General errors
    /// </summary>
    Error,

    /// <summary>
    /// Success notifications
    /// </summary>
    Success
}

/// <summary>
/// Represents a notification in the system
/// </summary>
public class Notification
{
    /// <summary>
    /// Unique identifier for the notification
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of notification
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Main notification message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Source of the notification (e.g., "Virtual Key ID: 123")
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Additional details about the notification
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the notification was created
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether the notification has been marked as read
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets a color class based on the notification type
    /// </summary>
    public string GetColorClass()
    {
        return Type switch
        {
            NotificationType.Error => "danger",
            NotificationType.Security => "danger",
            NotificationType.BudgetWarning => "warning",
            NotificationType.KeyExpiration => "warning",
            NotificationType.VirtualKeyValidation => "warning",
            NotificationType.System => "info",
            NotificationType.Success => "success",
            _ => "secondary"
        };
    }

    /// <summary>
    /// Gets an icon class based on the notification type
    /// </summary>
    public string GetIconClass()
    {
        return Type switch
        {
            NotificationType.Error => "bi-exclamation-triangle-fill",
            NotificationType.Security => "bi-shield-exclamation",
            NotificationType.BudgetWarning => "bi-cash-coin",
            NotificationType.KeyExpiration => "bi-clock-history",
            NotificationType.VirtualKeyValidation => "bi-key-fill",
            NotificationType.System => "bi-info-circle-fill",
            NotificationType.Success => "bi-check-circle-fill",
            _ => "bi-bell-fill"
        };
    }
}
