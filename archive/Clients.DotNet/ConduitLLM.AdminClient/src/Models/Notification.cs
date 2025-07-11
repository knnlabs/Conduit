using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a notification in the system.
/// </summary>
public class NotificationDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the notification.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the virtual key ID associated with this notification.
    /// </summary>
    public int? VirtualKeyId { get; set; }

    /// <summary>
    /// Gets or sets the virtual key name for display purposes.
    /// </summary>
    public string? VirtualKeyName { get; set; }

    /// <summary>
    /// Gets or sets the type of notification.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the notification.
    /// </summary>
    public NotificationSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the notification message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the notification has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets when the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents a request to create a new notification.
/// </summary>
public class CreateNotificationDto
{
    /// <summary>
    /// Gets or sets the virtual key ID to associate with this notification.
    /// </summary>
    public int? VirtualKeyId { get; set; }

    /// <summary>
    /// Gets or sets the type of notification.
    /// </summary>
    [Required]
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the notification.
    /// </summary>
    [Required]
    public NotificationSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the notification message content.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Represents a request to update an existing notification.
/// </summary>
public class UpdateNotificationDto
{
    /// <summary>
    /// Gets or sets the notification message content.
    /// </summary>
    [MaxLength(500)]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets whether the notification should be marked as read.
    /// </summary>
    public bool? IsRead { get; set; }
}

/// <summary>
/// Notification type enumeration.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Budget warning notification.
    /// </summary>
    BudgetWarning = 0,

    /// <summary>
    /// Expiration warning notification.
    /// </summary>
    ExpirationWarning = 1,

    /// <summary>
    /// System notification.
    /// </summary>
    System = 2
}

/// <summary>
/// Notification severity enumeration.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// Informational notification.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning notification.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error notification.
    /// </summary>
    Error = 2
}

/// <summary>
/// Notification filter options for queries.
/// </summary>
public class NotificationFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the notification type to filter by.
    /// </summary>
    public NotificationType? Type { get; set; }

    /// <summary>
    /// Gets or sets the notification severity to filter by.
    /// </summary>
    public NotificationSeverity? Severity { get; set; }

    /// <summary>
    /// Gets or sets whether to filter by read status.
    /// </summary>
    public bool? IsRead { get; set; }

    /// <summary>
    /// Gets or sets the virtual key ID to filter by.
    /// </summary>
    public int? VirtualKeyId { get; set; }

    /// <summary>
    /// Gets or sets the start date for filtering notifications.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for filtering notifications.
    /// </summary>
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Notification summary statistics.
/// </summary>
public class NotificationSummary
{
    /// <summary>
    /// Gets or sets the total number of notifications.
    /// </summary>
    public int TotalNotifications { get; set; }

    /// <summary>
    /// Gets or sets the number of unread notifications.
    /// </summary>
    public int UnreadNotifications { get; set; }

    /// <summary>
    /// Gets or sets the number of read notifications.
    /// </summary>
    public int ReadNotifications { get; set; }

    /// <summary>
    /// Gets or sets the number of notifications by type.
    /// </summary>
    public Dictionary<NotificationType, int> NotificationsByType { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of notifications by severity.
    /// </summary>
    public Dictionary<NotificationSeverity, int> NotificationsBySeverity { get; set; } = new();

    /// <summary>
    /// Gets or sets the most recent notification.
    /// </summary>
    public NotificationDto? MostRecentNotification { get; set; }

    /// <summary>
    /// Gets or sets the oldest unread notification.
    /// </summary>
    public NotificationDto? OldestUnreadNotification { get; set; }
}