using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// SignalR hub endpoints and connection paths.
/// </summary>
public static class SignalREndpoints
{
    /// <summary>
    /// Core task tracking hub for all async operations.
    /// </summary>
    public const string TaskHub = "/hubs/tasks";
    
    /// <summary>
    /// Video generation specific hub with progress tracking.
    /// </summary>
    public const string VideoGenerationHub = "/hubs/video-generation";
    
    /// <summary>
    /// Image generation specific hub with progress tracking.
    /// </summary>
    public const string ImageGenerationHub = "/hubs/image-generation";
    
    /// <summary>
    /// Unified content generation hub for images and videos.
    /// </summary>
    public const string ContentGenerationHub = "/hubs/content-generation";
    
    /// <summary>
    /// Spend and budget notification hub.
    /// </summary>
    public const string SpendNotificationHub = "/hubs/spend-notifications";
    
    /// <summary>
    /// System-wide notifications hub.
    /// </summary>
    public const string SystemNotificationHub = "/hubs/notifications";
    
    /// <summary>
    /// Model discovery and capability updates hub.
    /// </summary>
    public const string ModelDiscoveryHub = "/hubs/model-discovery";
    
    /// <summary>
    /// Webhook delivery tracking hub.
    /// </summary>
    public const string WebhookDeliveryHub = "/hubs/webhook-delivery";
}

#region Task Hub Models

/// <summary>
/// Task progress update notification.
/// </summary>
public class TaskProgressUpdate
{
    /// <summary>
    /// Unique task identifier.
    /// </summary>
    [Required]
    public string TaskId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }
    
    /// <summary>
    /// Optional progress message.
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Estimated seconds remaining for completion.
    /// </summary>
    public int? EstimatedSecondsRemaining { get; set; }
}

/// <summary>
/// Task completion notification.
/// </summary>
public class TaskCompletionUpdate
{
    /// <summary>
    /// Unique task identifier.
    /// </summary>
    [Required]
    public string TaskId { get; set; } = string.Empty;
    
    /// <summary>
    /// Task result data.
    /// </summary>
    public object? Result { get; set; }
    
    /// <summary>
    /// Task completion timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Task failure notification.
/// </summary>
public class TaskFailureUpdate
{
    /// <summary>
    /// Unique task identifier.
    /// </summary>
    [Required]
    public string TaskId { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message describing the failure.
    /// </summary>
    [Required]
    public string Error { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the task can be retried.
    /// </summary>
    public bool IsRetryable { get; set; }
    
    /// <summary>
    /// Failure timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region Spend Notification Models

/// <summary>
/// Spend update notification for virtual keys.
/// </summary>
public class SpendUpdateNotification
{
    /// <summary>
    /// Virtual key identifier.
    /// </summary>
    [Required]
    public string VirtualKeyId { get; set; } = string.Empty;
    
    /// <summary>
    /// Virtual key name.
    /// </summary>
    public string? VirtualKeyName { get; set; }
    
    /// <summary>
    /// Amount spent in this update.
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Current total spend for the virtual key.
    /// </summary>
    public decimal TotalSpend { get; set; }
    
    /// <summary>
    /// Maximum budget limit.
    /// </summary>
    public decimal? BudgetLimit { get; set; }
    
    /// <summary>
    /// Percentage of budget used.
    /// </summary>
    public decimal? BudgetPercentageUsed { get; set; }
    
    /// <summary>
    /// Spend update timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Budget alert notification when limits are approached or exceeded.
/// </summary>
public class BudgetAlertNotification
{
    /// <summary>
    /// Virtual key identifier.
    /// </summary>
    [Required]
    public string VirtualKeyId { get; set; } = string.Empty;
    
    /// <summary>
    /// Virtual key name.
    /// </summary>
    public string? VirtualKeyName { get; set; }
    
    /// <summary>
    /// Alert severity level.
    /// </summary>
    [Required]
    public string Severity { get; set; } = string.Empty;
    
    /// <summary>
    /// Alert message.
    /// </summary>
    [Required]
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Current spend amount.
    /// </summary>
    public decimal CurrentSpend { get; set; }
    
    /// <summary>
    /// Budget limit.
    /// </summary>
    public decimal BudgetLimit { get; set; }
    
    /// <summary>
    /// Percentage of budget used.
    /// </summary>
    public decimal PercentageUsed { get; set; }
    
    /// <summary>
    /// Alert timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region System Notification Models

/// <summary>
/// Base class for all system notifications.
/// </summary>
public abstract class SystemNotification
{
    /// <summary>
    /// Unique notification identifier.
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification type.
    /// </summary>
    [Required]
    public abstract string Type { get; }
    
    /// <summary>
    /// Notification title.
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification message.
    /// </summary>
    [Required]
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Severity level (info, warning, error, critical).
    /// </summary>
    [Required]
    public string Severity { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Provider health status change notification.
/// </summary>
public class ProviderHealthNotification : SystemNotification
{
    /// <summary>
    /// Notification type identifier.
    /// </summary>
    public override string Type => "provider_health";
    
    /// <summary>
    /// Provider name.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Previous health status.
    /// </summary>
    public string? PreviousStatus { get; set; }
    
    /// <summary>
    /// Current health status.
    /// </summary>
    [Required]
    public string CurrentStatus { get; set; } = string.Empty;
    
    /// <summary>
    /// Health check details.
    /// </summary>
    public object? HealthDetails { get; set; }
}

/// <summary>
/// Rate limit warning notification.
/// </summary>
public class RateLimitNotification : SystemNotification
{
    /// <summary>
    /// Notification type identifier.
    /// </summary>
    public override string Type => "rate_limit";
    
    /// <summary>
    /// Provider or service being rate limited.
    /// </summary>
    [Required]
    public string Service { get; set; } = string.Empty;
    
    /// <summary>
    /// Current request rate.
    /// </summary>
    public int CurrentRate { get; set; }
    
    /// <summary>
    /// Rate limit threshold.
    /// </summary>
    public int Limit { get; set; }
    
    /// <summary>
    /// Time window for the rate limit.
    /// </summary>
    public TimeSpan Window { get; set; }
}

#endregion

#region Webhook Delivery Models

/// <summary>
/// Webhook delivery attempt notification.
/// </summary>
public class WebhookDeliveryAttempt
{
    /// <summary>
    /// Unique webhook identifier.
    /// </summary>
    [Required]
    public string WebhookId { get; set; } = string.Empty;
    
    /// <summary>
    /// Delivery attempt identifier.
    /// </summary>
    [Required]
    public string DeliveryId { get; set; } = string.Empty;
    
    /// <summary>
    /// Target webhook URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Event type being delivered.
    /// </summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// Attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; set; }
    
    /// <summary>
    /// Related task identifier.
    /// </summary>
    public string? TaskId { get; set; }
    
    /// <summary>
    /// Task type.
    /// </summary>
    public string? TaskType { get; set; }
    
    /// <summary>
    /// Attempt timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Webhook delivery success notification.
/// </summary>
public class WebhookDeliverySuccess
{
    /// <summary>
    /// Unique webhook identifier.
    /// </summary>
    [Required]
    public string WebhookId { get; set; } = string.Empty;
    
    /// <summary>
    /// Target webhook URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Event type delivered.
    /// </summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// HTTP status code received.
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public double ResponseTimeMs { get; set; }
    
    /// <summary>
    /// Total number of attempts made.
    /// </summary>
    public int AttemptCount { get; set; }
    
    /// <summary>
    /// Related task identifier.
    /// </summary>
    public string? TaskId { get; set; }
    
    /// <summary>
    /// Success timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Webhook delivery failure notification.
/// </summary>
public class WebhookDeliveryFailure
{
    /// <summary>
    /// Unique webhook identifier.
    /// </summary>
    [Required]
    public string WebhookId { get; set; } = string.Empty;
    
    /// <summary>
    /// Target webhook URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Event type attempted.
    /// </summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message describing the failure.
    /// </summary>
    [Required]
    public string Error { get; set; } = string.Empty;
    
    /// <summary>
    /// HTTP status code if received.
    /// </summary>
    public int? StatusCode { get; set; }
    
    /// <summary>
    /// Total number of attempts made.
    /// </summary>
    public int AttemptCount { get; set; }
    
    /// <summary>
    /// Whether this is the final attempt.
    /// </summary>
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// Related task identifier.
    /// </summary>
    public string? TaskId { get; set; }
    
    /// <summary>
    /// Failure timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region Model Discovery Models

/// <summary>
/// Model discovery notification when new capabilities are found.
/// </summary>
public class ModelDiscoveryNotification
{
    /// <summary>
    /// Provider name where models were discovered.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of models discovered.
    /// </summary>
    public int ModelCount { get; set; }
    
    /// <summary>
    /// Discovery details.
    /// </summary>
    public object? DiscoveryDetails { get; set; }
    
    /// <summary>
    /// Discovery timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Provider capabilities update notification.
/// </summary>
public class ProviderCapabilitiesNotification
{
    /// <summary>
    /// Provider name.
    /// </summary>
    [Required]
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Updated capabilities.
    /// </summary>
    public object? Capabilities { get; set; }
    
    /// <summary>
    /// Update reason.
    /// </summary>
    public string? UpdateReason { get; set; }
    
    /// <summary>
    /// Update timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion