using System;
using System.Collections.Generic;

namespace ConduitLLM.Admin.Models.ErrorQueue
{
    /// <summary>
    /// Response containing list of error queues and summary information.
    /// </summary>
    public record ErrorQueueListResponse
    {
        /// <summary>
        /// List of error queues with their details.
        /// </summary>
        public List<ErrorQueueInfo> Queues { get; init; } = new();

        /// <summary>
        /// Summary statistics across all error queues.
        /// </summary>
        public ErrorQueueSummary Summary { get; init; } = new();

        /// <summary>
        /// Timestamp when this data was collected.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Information about a single error queue.
    /// </summary>
    public record ErrorQueueInfo
    {
        /// <summary>
        /// Full name of the error queue (e.g., "ProviderEventHandler_error").
        /// </summary>
        public string QueueName { get; init; } = string.Empty;

        /// <summary>
        /// Original queue name derived from error queue name.
        /// </summary>
        public string OriginalQueue { get; init; } = string.Empty;

        /// <summary>
        /// Number of messages in the queue.
        /// </summary>
        public long MessageCount { get; init; }

        /// <summary>
        /// Total size of messages in bytes.
        /// </summary>
        public long MessageBytes { get; init; }

        /// <summary>
        /// Number of consumers (should always be 0 for error queues).
        /// </summary>
        public int ConsumerCount { get; init; }

        /// <summary>
        /// Timestamp of the oldest message in the queue.
        /// </summary>
        public DateTime? OldestMessageTimestamp { get; init; }

        /// <summary>
        /// Timestamp of the newest message in the queue.
        /// </summary>
        public DateTime? NewestMessageTimestamp { get; init; }

        /// <summary>
        /// Rate of messages per minute.
        /// </summary>
        public double MessageRate { get; init; }

        /// <summary>
        /// Status of the queue: "ok", "warning", or "critical".
        /// </summary>
        public string Status { get; init; } = "ok";
    }

    /// <summary>
    /// Summary statistics across all error queues.
    /// </summary>
    public record ErrorQueueSummary
    {
        /// <summary>
        /// Total number of error queues.
        /// </summary>
        public int TotalQueues { get; init; }

        /// <summary>
        /// Total number of messages across all queues.
        /// </summary>
        public long TotalMessages { get; init; }

        /// <summary>
        /// Total size in bytes across all queues.
        /// </summary>
        public long TotalBytes { get; init; }

        /// <summary>
        /// List of queue names in critical state.
        /// </summary>
        public List<string> CriticalQueues { get; init; } = new();

        /// <summary>
        /// List of queue names in warning state.
        /// </summary>
        public List<string> WarningQueues { get; init; } = new();
    }

    /// <summary>
    /// Response containing paginated list of error messages.
    /// </summary>
    public record ErrorMessageListResponse
    {
        /// <summary>
        /// Name of the queue these messages are from.
        /// </summary>
        public string QueueName { get; init; } = string.Empty;

        /// <summary>
        /// List of error messages.
        /// </summary>
        public List<ErrorMessage> Messages { get; init; } = new();

        /// <summary>
        /// Current page number.
        /// </summary>
        public int Page { get; init; }

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; init; }

        /// <summary>
        /// Total number of messages in the queue.
        /// </summary>
        public long TotalMessages { get; init; }

        /// <summary>
        /// Total number of pages.
        /// </summary>
        public int TotalPages { get; init; }
    }

    /// <summary>
    /// Error message information.
    /// </summary>
    public record ErrorMessage
    {
        /// <summary>
        /// Unique message identifier.
        /// </summary>
        public string MessageId { get; init; } = string.Empty;

        /// <summary>
        /// Correlation ID for tracking related messages.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>
        /// Timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Type of the message.
        /// </summary>
        public string MessageType { get; init; } = string.Empty;

        /// <summary>
        /// Message headers.
        /// </summary>
        public Dictionary<string, object> Headers { get; init; } = new();

        /// <summary>
        /// Message body (JSON representation).
        /// </summary>
        public object? Body { get; init; }

        /// <summary>
        /// Error details.
        /// </summary>
        public ErrorDetails Error { get; init; } = new();

        /// <summary>
        /// Number of retry attempts.
        /// </summary>
        public int RetryCount { get; init; }
    }

    /// <summary>
    /// Detailed error message information.
    /// </summary>
    public record ErrorMessageDetail : ErrorMessage
    {
        /// <summary>
        /// Additional context about the failure.
        /// </summary>
        public Dictionary<string, object> Context { get; init; } = new();

        /// <summary>
        /// Full exception details if available.
        /// </summary>
        public string? FullException { get; init; }
    }

    /// <summary>
    /// Error details within a message.
    /// </summary>
    public record ErrorDetails
    {
        /// <summary>
        /// Type of the exception.
        /// </summary>
        public string ExceptionType { get; init; } = string.Empty;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Stack trace of the error.
        /// </summary>
        public string? StackTrace { get; init; }

        /// <summary>
        /// Timestamp when the failure occurred.
        /// </summary>
        public DateTime FailedAt { get; init; }
    }

    /// <summary>
    /// Error queue statistics and trends.
    /// </summary>
    public record ErrorQueueStatistics
    {
        /// <summary>
        /// Start date of the statistics period.
        /// </summary>
        public DateTime Since { get; init; }

        /// <summary>
        /// End date of the statistics period.
        /// </summary>
        public DateTime Until { get; init; }

        /// <summary>
        /// Grouping interval used.
        /// </summary>
        public string GroupBy { get; init; } = "hour";

        /// <summary>
        /// Error rate trends over time.
        /// </summary>
        public List<ErrorRateTrend> ErrorRateTrends { get; init; } = new();

        /// <summary>
        /// Top failing message types.
        /// </summary>
        public List<FailingMessageType> TopFailingMessageTypes { get; init; } = new();

        /// <summary>
        /// Queue growth patterns.
        /// </summary>
        public List<QueueGrowthPattern> QueueGrowthPatterns { get; init; } = new();

        /// <summary>
        /// Average message age in hours.
        /// </summary>
        public double AverageMessageAgeHours { get; init; }

        /// <summary>
        /// Total error count in the period.
        /// </summary>
        public long TotalErrors { get; init; }
    }

    /// <summary>
    /// Error rate trend data point.
    /// </summary>
    public record ErrorRateTrend
    {
        /// <summary>
        /// Time period for this data point.
        /// </summary>
        public DateTime Period { get; init; }

        /// <summary>
        /// Number of errors in this period.
        /// </summary>
        public int ErrorCount { get; init; }

        /// <summary>
        /// Error rate per minute.
        /// </summary>
        public double ErrorsPerMinute { get; init; }
    }

    /// <summary>
    /// Information about a failing message type.
    /// </summary>
    public record FailingMessageType
    {
        /// <summary>
        /// Message type name.
        /// </summary>
        public string MessageType { get; init; } = string.Empty;

        /// <summary>
        /// Number of failures.
        /// </summary>
        public int FailureCount { get; init; }

        /// <summary>
        /// Percentage of total failures.
        /// </summary>
        public double Percentage { get; init; }

        /// <summary>
        /// Most common error for this message type.
        /// </summary>
        public string MostCommonError { get; init; } = string.Empty;
    }

    /// <summary>
    /// Queue growth pattern information.
    /// </summary>
    public record QueueGrowthPattern
    {
        /// <summary>
        /// Queue name.
        /// </summary>
        public string QueueName { get; init; } = string.Empty;

        /// <summary>
        /// Growth rate (messages per hour).
        /// </summary>
        public double GrowthRate { get; init; }

        /// <summary>
        /// Trend direction: "increasing", "decreasing", or "stable".
        /// </summary>
        public string Trend { get; init; } = "stable";

        /// <summary>
        /// Current message count.
        /// </summary>
        public long CurrentCount { get; init; }
    }

    /// <summary>
    /// Error queue health status.
    /// </summary>
    public record ErrorQueueHealth
    {
        /// <summary>
        /// Overall health status: "healthy", "degraded", or "unhealthy".
        /// </summary>
        public string Status { get; init; } = "healthy";

        /// <summary>
        /// Health check timestamp.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Number of queues in each status.
        /// </summary>
        public HealthStatusCounts StatusCounts { get; init; } = new();

        /// <summary>
        /// List of issues found.
        /// </summary>
        public List<HealthIssue> Issues { get; init; } = new();

        /// <summary>
        /// Overall health score (0-100).
        /// </summary>
        public int HealthScore { get; init; }
    }

    /// <summary>
    /// Counts of queues in each health status.
    /// </summary>
    public record HealthStatusCounts
    {
        /// <summary>
        /// Number of healthy queues.
        /// </summary>
        public int Healthy { get; init; }

        /// <summary>
        /// Number of queues with warnings.
        /// </summary>
        public int Warning { get; init; }

        /// <summary>
        /// Number of critical queues.
        /// </summary>
        public int Critical { get; init; }
    }

    /// <summary>
    /// Health issue description.
    /// </summary>
    public record HealthIssue
    {
        /// <summary>
        /// Severity of the issue: "warning" or "critical".
        /// </summary>
        public string Severity { get; init; } = "warning";

        /// <summary>
        /// Queue name affected.
        /// </summary>
        public string QueueName { get; init; } = string.Empty;

        /// <summary>
        /// Description of the issue.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Suggested action to resolve.
        /// </summary>
        public string? SuggestedAction { get; init; }
    }
}