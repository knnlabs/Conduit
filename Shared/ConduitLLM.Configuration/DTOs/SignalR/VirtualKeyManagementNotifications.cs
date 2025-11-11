namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification for when a new virtual key is created.
    /// </summary>
    public class VirtualKeyCreatedNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Gets or sets the virtual key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the key is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum budget if configured.
        /// </summary>
        public decimal? MaxBudget { get; set; }

        /// <summary>
        /// Gets or sets the allowed models list.
        /// </summary>
        public string? AllowedModels { get; set; }

        /// <summary>
        /// Gets or sets the rate limit per minute.
        /// </summary>
        public int? RateLimitPerMinute { get; set; }

        /// <summary>
        /// Gets or sets when the key was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the key expires.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification for when a virtual key is updated.
    /// </summary>
    public class VirtualKeyUpdatedNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Gets or sets the virtual key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of properties that were updated.
        /// </summary>
        public List<string> UpdatedProperties { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the key is enabled (if updated).
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum budget (if updated).
        /// </summary>
        public decimal? MaxBudget { get; set; }

        /// <summary>
        /// Gets or sets the allowed models (if updated).
        /// </summary>
        public string? AllowedModels { get; set; }

        /// <summary>
        /// Gets or sets the rate limit (if updated).
        /// </summary>
        public int? RateLimitPerMinute { get; set; }

        /// <summary>
        /// Gets or sets the expiration date (if updated).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the update.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification for when a virtual key is deleted.
    /// </summary>
    public class VirtualKeyDeletedNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Gets or sets the virtual key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the key was deleted.
        /// </summary>
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the reason for deletion if provided.
        /// </summary>
        public string? DeletionReason { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification for when a virtual key's status changes.
    /// </summary>
    public class VirtualKeyStatusChangedNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Gets or sets the virtual key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous status.
        /// </summary>
        public string PreviousStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new status.
        /// </summary>
        public string NewStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for the status change.
        /// </summary>
        public string? StatusChangeReason { get; set; }

        /// <summary>
        /// Gets or sets when the status changed.
        /// </summary>
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification containing the current status of a virtual key.
    /// </summary>
    public class VirtualKeyStatusNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int KeyId { get; set; }

        /// <summary>
        /// Gets or sets the virtual key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the key is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the current spend amount.
        /// </summary>
        public decimal CurrentSpend { get; set; }

        /// <summary>
        /// Gets or sets the maximum budget.
        /// </summary>
        public decimal? MaxBudget { get; set; }

        /// <summary>
        /// Gets or sets the budget usage percentage.
        /// </summary>
        public double? BudgetPercentage { get; set; }

        /// <summary>
        /// Gets or sets the allowed models.
        /// </summary>
        public string? AllowedModels { get; set; }

        /// <summary>
        /// Gets or sets the rate limit per minute.
        /// </summary>
        public int? RateLimitPerMinute { get; set; }

        /// <summary>
        /// Gets or sets when the key was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the key was last used.
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Gets or sets when the key expires.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}