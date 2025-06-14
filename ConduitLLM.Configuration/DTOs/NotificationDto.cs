using System;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for notifications
    /// </summary>
    public class NotificationDto
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the virtual key related to this notification (if any)
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Name of the virtual key (if applicable)
        /// </summary>
        public string? VirtualKeyName { get; set; }

        /// <summary>
        /// Type of the notification
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// Severity level of the notification
        /// </summary>
        public NotificationSeverity Severity { get; set; }

        /// <summary>
        /// Message text of the notification
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Whether the notification has been read
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// Timestamp when the notification was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Data transfer object for creating a new notification
    /// </summary>
    public class CreateNotificationDto
    {
        /// <summary>
        /// ID of the virtual key related to this notification (if any)
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Type of the notification
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// Severity level of the notification
        /// </summary>
        public NotificationSeverity Severity { get; set; }

        /// <summary>
        /// Message text of the notification
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data transfer object for updating a notification
    /// </summary>
    public class UpdateNotificationDto
    {
        /// <summary>
        /// ID of the notification to update
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Whether the notification has been read
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// Optional new message text
        /// </summary>
        public string? Message { get; set; }
    }
}
