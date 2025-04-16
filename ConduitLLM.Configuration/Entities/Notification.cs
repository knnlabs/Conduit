using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Type of notification
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// A budget warning notification
        /// </summary>
        BudgetWarning,
        
        /// <summary>
        /// An expiration warning notification
        /// </summary>
        ExpirationWarning,
        
        /// <summary>
        /// A system notification
        /// </summary>
        System
    }
    
    /// <summary>
    /// Severity level of notification
    /// </summary>
    public enum NotificationSeverity
    {
        /// <summary>
        /// Informational notification
        /// </summary>
        Info,
        
        /// <summary>
        /// Warning notification
        /// </summary>
        Warning,
        
        /// <summary>
        /// Error or critical notification
        /// </summary>
        Error
    }
    
    /// <summary>
    /// Represents a notification related to virtual keys
    /// </summary>
    public class Notification
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// ID of the virtual key related to this notification
        /// </summary>
        public int? VirtualKeyId { get; set; }
        
        /// <summary>
        /// Foreign key relationship to the virtual key
        /// </summary>
        [ForeignKey("VirtualKeyId")]
        public virtual VirtualKey? VirtualKey { get; set; }
        
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
        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the notification has been read
        /// </summary>
        public bool IsRead { get; set; }
        
        /// <summary>
        /// Timestamp when the notification was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
