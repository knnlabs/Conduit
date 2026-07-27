namespace ConduitLLM.Core.Models.SignalR
{
    /// <summary>
    /// Base class for SignalR messages with acknowledgment support
    /// </summary>
    public abstract class SignalRMessage
    {
        /// <summary>
        /// Unique message ID for tracking and acknowledgment
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracing related messages
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Retry count if message delivery fails
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Type of the message for routing and processing.
        /// Override in derived classes to specify the message type.
        /// </summary>
        public virtual string MessageType => GetType().Name;

        /// <summary>
        /// Priority of the message (higher values = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Indicates if this is a critical message that must be delivered
        /// </summary>
        public bool IsCritical { get; set; } = false;

        /// <summary>
        /// Expiration time for the message (null = no expiration)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Checks if the message has expired
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }

    /// <summary>
    /// Message for task progress updates that require acknowledgment.
    /// </summary>
    public class TaskProgressMessage : SignalRMessage
    {
        public override string MessageType => "TaskProgress";

        /// <summary>
        /// ID of the task.
        /// </summary>
        public string TaskId { get; set; } = null!;

        /// <summary>
        /// Current progress percentage (0-100).
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Human-readable status message.
        /// </summary>
        public string StatusMessage { get; set; } = null!;

        /// <summary>
        /// Additional metadata about the progress.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Estimated time remaining in seconds.
        /// </summary>
        public int? EstimatedSecondsRemaining { get; set; }

        public TaskProgressMessage()
        {
            IsCritical = true;
            Priority = 5;
        }
    }
}
