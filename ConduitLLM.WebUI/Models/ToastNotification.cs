namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Represents a transient toast notification for user feedback.
    /// </summary>
    public class ToastNotification
    {
        /// <summary>
        /// Gets or sets the unique identifier for the notification.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the notification message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notification title (optional).
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the notification.
        /// </summary>
        public ToastSeverity Severity { get; set; } = ToastSeverity.Info;

        /// <summary>
        /// Gets or sets the timestamp when the notification was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the duration in milliseconds before auto-dismiss (0 = no auto-dismiss).
        /// </summary>
        public int DurationMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets whether the notification can be manually dismissed.
        /// </summary>
        public bool IsDismissible { get; set; } = true;

        /// <summary>
        /// Gets or sets optional action button text.
        /// </summary>
        public string? ActionText { get; set; }

        /// <summary>
        /// Gets or sets optional action callback.
        /// </summary>
        public Action? ActionCallback { get; set; }

        /// <summary>
        /// Gets or sets additional CSS classes for custom styling.
        /// </summary>
        public string? AdditionalCssClass { get; set; }

        /// <summary>
        /// Gets or sets whether the notification is currently being dismissed (for animation).
        /// </summary>
        public bool IsDismissing { get; set; }
    }

    /// <summary>
    /// Defines the severity levels for toast notifications.
    /// </summary>
    public enum ToastSeverity
    {
        /// <summary>
        /// Informational message.
        /// </summary>
        Info,

        /// <summary>
        /// Success message.
        /// </summary>
        Success,

        /// <summary>
        /// Warning message.
        /// </summary>
        Warning,

        /// <summary>
        /// Error message.
        /// </summary>
        Error
    }
}
