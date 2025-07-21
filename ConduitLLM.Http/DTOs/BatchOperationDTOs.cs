using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Http.DTOs
{
    /// <summary>
    /// Response when starting a batch operation
    /// </summary>
    public class BatchOperationStartResponse
    {
        /// <summary>
        /// Unique identifier for tracking the operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Total number of items in the batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// URL to check operation status
        /// </summary>
        public string StatusUrl { get; set; } = string.Empty;

        /// <summary>
        /// SignalR task ID to subscribe to for real-time updates
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// SignalR event names to listen for
        /// </summary>
        public List<string> SignalREvents { get; set; } = new()
        {
            "BatchOperationStarted",
            "BatchOperationProgress",
            "BatchOperationCompleted",
            "BatchOperationFailed",
            "BatchOperationCancelled",
            "BatchItemCompleted"
        };

        /// <summary>
        /// Informational message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Current status of a batch operation
    /// </summary>
    public class BatchOperationStatusResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Total items in batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Items processed so far
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Successful items
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Failed items
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Time elapsed
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Processing rate
        /// </summary>
        public double ItemsPerSecond { get; set; }

        /// <summary>
        /// Current item being processed
        /// </summary>
        public string? CurrentItem { get; set; }

        /// <summary>
        /// Whether operation can be cancelled
        /// </summary>
        public bool CanCancel { get; set; }
    }

    /// <summary>
    /// Request to update spend for multiple virtual keys
    /// </summary>
    public class BatchSpendUpdateRequest
    {
        /// <summary>
        /// List of spend updates to process
        /// </summary>
        [Required]
        public List<SpendUpdateDto> Updates { get; set; } = new();
    }

    /// <summary>
    /// Individual spend update item
    /// </summary>
    public class SpendUpdateDto
    {
        /// <summary>
        /// Virtual key to update
        /// </summary>
        [Required]
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Amount to add to spend
        /// </summary>
        [Required]
        [Range(0.0001, 1000000)]
        public decimal Amount { get; set; }

        /// <summary>
        /// Model used
        /// </summary>
        [Required]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Provider used
        /// </summary>
        [Required]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Request to update multiple virtual keys
    /// </summary>
    public class BatchVirtualKeyUpdateRequest
    {
        /// <summary>
        /// List of virtual key updates
        /// </summary>
        [Required]
        public List<VirtualKeyUpdateDto> Updates { get; set; } = new();
    }

    /// <summary>
    /// Individual virtual key update
    /// </summary>
    public class VirtualKeyUpdateDto
    {
        /// <summary>
        /// Virtual key ID to update
        /// </summary>
        [Required]
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// New budget limit (optional)
        /// </summary>
        [Range(0, 1000000)]
        public decimal? MaxBudget { get; set; }

        /// <summary>
        /// New allowed models list (optional)
        /// </summary>
        public List<string>? AllowedModels { get; set; }

        /// <summary>
        /// New rate limits (optional)
        /// </summary>
        public Dictionary<string, object>? RateLimits { get; set; }

        /// <summary>
        /// Enable/disable key (optional)
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// New expiry date (optional)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Notes about the update
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to send webhooks in batch
    /// </summary>
    public class BatchWebhookSendRequest
    {
        /// <summary>
        /// List of webhooks to send
        /// </summary>
        [Required]
        public List<WebhookSendDto> Webhooks { get; set; } = new();
    }

    /// <summary>
    /// Individual webhook to send
    /// </summary>
    public class WebhookSendDto
    {
        /// <summary>
        /// Webhook URL
        /// </summary>
        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Event type
        /// </summary>
        [Required]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Payload to send
        /// </summary>
        [Required]
        public object Payload { get; set; } = new { };

        /// <summary>
        /// Custom headers
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Webhook secret for signature
        /// </summary>
        public string? Secret { get; set; }
    }
}