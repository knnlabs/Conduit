using System;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Base class for all SignalR messages that require acknowledgment
    /// </summary>
    public abstract class SignalRMessage
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional correlation ID for tracking related messages
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Number of times this message has been retried
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Type of the message for routing and processing
        /// </summary>
        public abstract string MessageType { get; }

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
}