namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification sent when a batch operation starts
    /// </summary>
    public class BatchOperationStartedNotification
    {
        /// <summary>
        /// Unique identifier for the batch operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation (e.g., "spend_update", "virtual_key_update", "webhook_send")
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Total number of items to process
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Virtual key ID that initiated the operation
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Maximum degree of parallelism being used
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Whether the operation supports cancellation
        /// </summary>
        public bool SupportsCancellation { get; set; }

        /// <summary>
        /// Whether the operation supports resumption after failures
        /// </summary>
        public bool SupportsResume { get; set; }

        /// <summary>
        /// Timestamp when the operation started
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional metadata about the operation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Notification sent when batch operation progress updates
    /// </summary>
    public class BatchOperationProgressNotification
    {
        /// <summary>
        /// Unique identifier for the batch operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Number of items processed so far
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Number of successful items
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of failed items
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Current processing rate (items per second)
        /// </summary>
        public double ItemsPerSecond { get; set; }

        /// <summary>
        /// Time elapsed since operation started
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Description of the current item being processed
        /// </summary>
        public string? CurrentItem { get; set; }

        /// <summary>
        /// Optional progress message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Timestamp of this update
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification sent when a batch operation completes
    /// </summary>
    public class BatchOperationCompletedNotification
    {
        /// <summary>
        /// Unique identifier for the batch operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Final status of the operation
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Total number of items processed
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Number of successful items
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of failed items
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Total duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Average processing rate (items per second)
        /// </summary>
        public double AverageItemsPerSecond { get; set; }

        /// <summary>
        /// Timestamp when the operation completed
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Summary of errors if any
        /// </summary>
        public List<BatchOperationError> Errors { get; set; } = new();

        /// <summary>
        /// Result summary data
        /// </summary>
        public object? ResultSummary { get; set; }
    }

    /// <summary>
    /// Notification sent when a batch operation fails
    /// </summary>
    public class BatchOperationFailedNotification
    {
        /// <summary>
        /// Unique identifier for the batch operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Error message describing the failure
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Whether the operation can be retried
        /// </summary>
        public bool IsRetryable { get; set; }

        /// <summary>
        /// Number of items processed before failure
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Number of items that failed
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Timestamp when the operation failed
        /// </summary>
        public DateTime FailedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stack trace if available
        /// </summary>
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// Notification sent when a batch operation is cancelled
    /// </summary>
    public class BatchOperationCancelledNotification
    {
        /// <summary>
        /// Unique identifier for the batch operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Reason for cancellation
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Number of items processed before cancellation
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Number of items remaining
        /// </summary>
        public int RemainingCount { get; set; }

        /// <summary>
        /// Whether the operation can be resumed
        /// </summary>
        public bool CanResume { get; set; }

        /// <summary>
        /// Timestamp when the operation was cancelled
        /// </summary>
        public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents an error that occurred during batch processing
    /// </summary>
    public class BatchOperationError
    {
        /// <summary>
        /// Index of the item that failed
        /// </summary>
        public int ItemIndex { get; set; }

        /// <summary>
        /// Identifier for the failed item
        /// </summary>
        public string? ItemIdentifier { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime ErrorTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification for batch operation item completion
    /// </summary>
    public class BatchOperationItemCompletedNotification
    {
        /// <summary>
        /// Unique identifier for the batch operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Index of the completed item
        /// </summary>
        public int ItemIndex { get; set; }

        /// <summary>
        /// Identifier for the completed item
        /// </summary>
        public string? ItemIdentifier { get; set; }

        /// <summary>
        /// Whether the item was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Processing duration for this item
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Result data from processing
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Timestamp when the item completed
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }
}