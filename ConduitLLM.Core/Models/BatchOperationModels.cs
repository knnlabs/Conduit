using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Options for configuring batch operations
    /// </summary>
    public class BatchOperationOptions
    {
        /// <summary>
        /// Virtual key ID for authorization and tracking
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Maximum number of concurrent operations
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Whether to continue processing on individual item errors
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Enable checkpointing for resumable operations
        /// </summary>
        public bool EnableCheckpointing { get; set; } = false;

        /// <summary>
        /// Number of items between checkpoints
        /// </summary>
        public int CheckpointInterval { get; set; } = 100;

        /// <summary>
        /// Timeout for individual item processing
        /// </summary>
        public TimeSpan? ItemTimeout { get; set; }

        /// <summary>
        /// Custom metadata for the operation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of processing a single batch item
    /// </summary>
    public class BatchItemResult
    {
        /// <summary>
        /// Whether the item was processed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Unique identifier for the item
        /// </summary>
        public string? ItemIdentifier { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Processing duration for this item
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Result data from processing
        /// </summary>
        public object? Data { get; set; }
    }

    /// <summary>
    /// Overall result of a batch operation
    /// </summary>
    public class BatchOperationResult
    {
        /// <summary>
        /// Unique identifier for the operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Final status of the operation
        /// </summary>
        public BatchOperationStatusEnum Status { get; set; }

        /// <summary>
        /// Total number of items in the batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Number of successfully processed items
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
        /// Average processing rate
        /// </summary>
        public double ItemsPerSecond { get; set; }

        /// <summary>
        /// List of errors encountered
        /// </summary>
        public List<BatchItemError> Errors { get; set; } = new();

        /// <summary>
        /// Detailed results for each processed item
        /// </summary>
        public List<BatchItemResult> ProcessedItems { get; set; } = new();

        /// <summary>
        /// Timestamp when the operation started
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the operation completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Error information for a failed batch item
    /// </summary>
    public class BatchItemError
    {
        /// <summary>
        /// Index of the item in the batch
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
        /// Stack trace if available
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Timestamp of the error
        /// </summary>
        public DateTime ErrorTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Current status of a batch operation
    /// </summary>
    public class BatchOperationStatus
    {
        /// <summary>
        /// Unique identifier for the operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Total number of items
        /// </summary>
        public int TotalItems { get; set; }

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
        /// Time elapsed since operation started
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Current processing rate
        /// </summary>
        public double ItemsPerSecond { get; set; }

        /// <summary>
        /// Current status of the operation
        /// </summary>
        public BatchOperationStatusEnum Status { get; set; }

        /// <summary>
        /// Description of the current item being processed
        /// </summary>
        public string? CurrentItem { get; set; }

        /// <summary>
        /// Whether the operation can be cancelled
        /// </summary>
        public bool CanCancel { get; set; } = true;

        /// <summary>
        /// Whether the operation can be resumed
        /// </summary>
        public bool CanResume { get; set; }
    }

    /// <summary>
    /// Status values for batch operations
    /// </summary>
    public enum BatchOperationStatusEnum
    {
        /// <summary>
        /// Operation is queued but not started
        /// </summary>
        Queued,

        /// <summary>
        /// Operation is currently running
        /// </summary>
        Running,

        /// <summary>
        /// Operation is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Operation completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Operation failed
        /// </summary>
        Failed,

        /// <summary>
        /// Operation was cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// Operation partially completed with some failures
        /// </summary>
        PartiallyCompleted
    }
}