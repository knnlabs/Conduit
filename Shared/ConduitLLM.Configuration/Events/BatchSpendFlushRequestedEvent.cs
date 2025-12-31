using MassTransit;

namespace ConduitLLM.Configuration.Events
{
    /// <summary>
    /// Event raised when an administrator requests immediate flushing of pending batch spend updates.
    /// This event triggers the BatchSpendUpdateService to process all queued spending charges immediately
    /// rather than waiting for the scheduled flush interval.
    /// </summary>
    public class BatchSpendFlushRequestedEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier for this flush request.
        /// Used for tracking and correlation across logs and metrics.
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets who requested the flush operation.
        /// Typically "Admin" for administrative operations or "Integration Test" for test scenarios.
        /// </summary>
        public string RequestedBy { get; set; } = "System";

        /// <summary>
        /// Gets or sets when the flush was requested.
        /// Used for auditing and performance tracking.
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the reason for the flush request.
        /// Provides context for operational tracking and debugging.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the source system that initiated the request.
        /// Examples: "Admin API", "Integration Test", "Health Monitor"
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the priority level of this flush request.
        /// Normal = standard administrative flush, High = urgent operational need
        /// </summary>
        public FlushPriority Priority { get; set; } = FlushPriority.Normal;

        /// <summary>
        /// Gets or sets the timeout for the flush operation in seconds.
        /// If not specified, uses the service default timeout.
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether to include detailed statistics in the response.
        /// When true, the consumer will return detailed metrics about what was flushed.
        /// </summary>
        public bool IncludeStatistics { get; set; } = true;
    }

    /// <summary>
    /// Priority levels for batch spend flush requests.
    /// </summary>
    public enum FlushPriority
    {
        /// <summary>
        /// Normal priority - standard administrative flush operation
        /// </summary>
        Normal = 0,

        /// <summary>
        /// High priority - urgent operational need (e.g., before maintenance)
        /// </summary>
        High = 1
    }

    /// <summary>
    /// Event consumer interface for batch spend flush requests.
    /// Implementations should handle the immediate processing of pending spend updates.
    /// </summary>
    public interface IBatchSpendFlushRequestedConsumer : IConsumer<BatchSpendFlushRequestedEvent>
    {
    }

    /// <summary>
    /// Response event published after a batch spend flush operation completes.
    /// Contains the results and statistics from the flush operation.
    /// </summary>
    public class BatchSpendFlushCompletedEvent
    {
        /// <summary>
        /// Gets or sets the request ID that this response corresponds to.
        /// Links the response back to the original flush request.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the flush operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the number of virtual key groups that were flushed.
        /// Zero indicates no pending charges were found.
        /// </summary>
        public int GroupsFlushed { get; set; }

        /// <summary>
        /// Gets or sets the total amount of pending charges that were processed.
        /// Useful for financial reconciliation and auditing.
        /// </summary>
        public decimal TotalAmountFlushed { get; set; }

        /// <summary>
        /// Gets or sets how long the flush operation took to complete.
        /// Used for performance monitoring and capacity planning.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets when the flush operation completed.
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// Null or empty when Success is true.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets additional statistics about the flush operation.
        /// Only populated when IncludeStatistics was true in the request.
        /// </summary>
        public BatchSpendFlushStatistics? Statistics { get; set; }
    }

    /// <summary>
    /// Detailed statistics about a batch spend flush operation.
    /// </summary>
    public class BatchSpendFlushStatistics
    {
        /// <summary>
        /// Gets or sets the number of Redis keys that were processed.
        /// </summary>
        public int RedisKeysProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of database transactions that were created.
        /// </summary>
        public int DatabaseTransactionsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of cache invalidation events that were triggered.
        /// </summary>
        public int CacheInvalidationsTriggered { get; set; }

        /// <summary>
        /// Gets or sets the Redis operation duration in milliseconds.
        /// </summary>
        public double RedisOperationMs { get; set; }

        /// <summary>
        /// Gets or sets the database operation duration in milliseconds.
        /// </summary>
        public double DatabaseOperationMs { get; set; }

        /// <summary>
        /// Gets or sets any warnings encountered during the flush operation.
        /// </summary>
        public string[]? Warnings { get; set; }
    }
}