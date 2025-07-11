using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Batch operation status enumeration
/// </summary>
public enum BatchOperationStatusEnum
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    PartiallyCompleted
}

/// <summary>
/// Request for batch spend updates
/// </summary>
public class BatchSpendUpdateRequest
{
    /// <summary>
    /// List of spend updates to process (max 10,000 items)
    /// </summary>
    [Required]
    public List<SpendUpdateDto> SpendUpdates { get; set; } = new();
}

/// <summary>
/// Individual spend update item
/// </summary>
public class SpendUpdateDto
{
    /// <summary>
    /// Virtual key ID to update spend for
    /// </summary>
    [Required]
    public int VirtualKeyId { get; set; }

    /// <summary>
    /// Amount to add to the spend (0.0001 to 1,000,000)
    /// </summary>
    [Required]
    [Range(0.0001, 1000000)]
    public decimal Amount { get; set; }

    /// <summary>
    /// Model name associated with the spend
    /// </summary>
    [Required]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Provider name associated with the spend
    /// </summary>
    [Required]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Optional metadata for the spend update
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request for batch virtual key updates
/// </summary>
public class BatchVirtualKeyUpdateRequest
{
    /// <summary>
    /// List of virtual key updates to process (max 1,000 items)
    /// </summary>
    [Required]
    public List<VirtualKeyUpdateDto> VirtualKeyUpdates { get; set; } = new();
}

/// <summary>
/// Individual virtual key update item
/// </summary>
public class VirtualKeyUpdateDto
{
    /// <summary>
    /// Virtual key ID to update
    /// </summary>
    [Required]
    public int VirtualKeyId { get; set; }

    /// <summary>
    /// New maximum budget for the virtual key
    /// </summary>
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// New list of allowed models
    /// </summary>
    public List<string>? AllowedModels { get; set; }

    /// <summary>
    /// New rate limits configuration
    /// </summary>
    public Dictionary<string, object>? RateLimits { get; set; }

    /// <summary>
    /// Whether the virtual key is enabled
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// New expiration date for the virtual key
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// New notes for the virtual key
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Request for batch webhook sends
/// </summary>
public class BatchWebhookSendRequest
{
    /// <summary>
    /// List of webhook sends to process (max 5,000 items)
    /// </summary>
    [Required]
    public List<WebhookSendDto> WebhookSends { get; set; } = new();
}

/// <summary>
/// Individual webhook send item
/// </summary>
public class WebhookSendDto
{
    /// <summary>
    /// Webhook URL to send to
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Event type for the webhook
    /// </summary>
    [Required]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Payload to send in the webhook
    /// </summary>
    [Required]
    public Dictionary<string, object> Payload { get; set; } = new();

    /// <summary>
    /// Optional headers to include in the webhook request
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Optional secret for webhook signature verification
    /// </summary>
    public string? Secret { get; set; }
}

/// <summary>
/// Response when starting a batch operation
/// </summary>
public class BatchOperationStartResponse
{
    /// <summary>
    /// Unique identifier for the batch operation
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// Task ID for SignalR real-time updates
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// URL to check operation status
    /// </summary>
    public string StatusUrl { get; set; } = string.Empty;

    /// <summary>
    /// Current operation status
    /// </summary>
    public BatchOperationStatusEnum Status { get; set; }

    /// <summary>
    /// When the operation was started
    /// </summary>
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Response containing batch operation status
/// </summary>
public class BatchOperationStatusResponse
{
    /// <summary>
    /// Unique identifier for the batch operation
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// Current operation status
    /// </summary>
    public BatchOperationStatusEnum Status { get; set; }

    /// <summary>
    /// Progress metadata
    /// </summary>
    public BatchOperationMetadata Metadata { get; set; } = new();

    /// <summary>
    /// List of individual item results
    /// </summary>
    public List<BatchItemResult> Results { get; set; } = new();

    /// <summary>
    /// List of errors that occurred during processing
    /// </summary>
    public List<BatchItemError> Errors { get; set; } = new();

    /// <summary>
    /// Whether the operation can be cancelled
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// Additional operation details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Batch operation metadata
/// </summary>
public class BatchOperationMetadata
{
    /// <summary>
    /// Total number of items in the batch
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Number of successfully processed items
    /// </summary>
    public int ProcessedItems { get; set; }

    /// <summary>
    /// Number of failed items
    /// </summary>
    public int FailedItems { get; set; }

    /// <summary>
    /// Processing rate in items per second
    /// </summary>
    public double ItemsPerSecond { get; set; }

    /// <summary>
    /// Estimated time of completion
    /// </summary>
    public DateTime? EstimatedCompletion { get; set; }

    /// <summary>
    /// Operation start time
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Operation completion time
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Batch item processing result
/// </summary>
public class BatchItemResult
{
    /// <summary>
    /// Item index in the batch
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Whether the item was processed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if processing failed
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Processing timestamp
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Batch item error details
/// </summary>
public class BatchItemError
{
    /// <summary>
    /// Item index in the batch
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error code
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Exception type if available
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Timestamp when error occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
}