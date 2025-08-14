using System;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Represents a message queued for delivery
    /// </summary>
    public class QueuedMessage
    {
        /// <summary>
        /// The message to be delivered
        /// </summary>
        public SignalRMessage Message { get; set; } = null!;

        /// <summary>
        /// Target connection ID (null for group messages)
        /// </summary>
        public string? ConnectionId { get; set; }

        /// <summary>
        /// Target group name (null for direct messages)
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Hub name for routing
        /// </summary>
        public string HubName { get; set; } = null!;

        /// <summary>
        /// Method name to invoke
        /// </summary>
        public string MethodName { get; set; } = null!;

        /// <summary>
        /// Time when the message was queued
        /// </summary>
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time when the next delivery attempt should be made
        /// </summary>
        public DateTime NextDeliveryAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of delivery attempts made
        /// </summary>
        public int DeliveryAttempts { get; set; }

        /// <summary>
        /// Last error encountered during delivery
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Last time a delivery was attempted
        /// </summary>
        public DateTime? LastAttemptAt { get; set; }

        /// <summary>
        /// Timeout for acknowledgment
        /// </summary>
        public TimeSpan AcknowledgmentTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Indicates if this is a dead letter message
        /// </summary>
        public bool IsDeadLetter { get; set; }

        /// <summary>
        /// Reason for moving to dead letter queue
        /// </summary>
        public string? DeadLetterReason { get; set; }
    }
}