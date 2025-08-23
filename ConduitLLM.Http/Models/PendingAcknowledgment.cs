namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Tracks pending acknowledgments for messages
    /// </summary>
    public class PendingAcknowledgment
    {
        /// <summary>
        /// The message awaiting acknowledgment
        /// </summary>
        public SignalRMessage Message { get; set; } = null!;

        /// <summary>
        /// Connection ID that the message was sent to
        /// </summary>
        public string ConnectionId { get; set; } = null!;

        /// <summary>
        /// Hub name where the message was sent
        /// </summary>
        public string HubName { get; set; } = null!;

        /// <summary>
        /// Method name that was invoked
        /// </summary>
        public string MethodName { get; set; } = null!;

        /// <summary>
        /// Time when the message was sent
        /// </summary>
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time when acknowledgment is expected by
        /// </summary>
        public DateTime TimeoutAt { get; set; }

        /// <summary>
        /// Current status of the acknowledgment
        /// </summary>
        public AcknowledgmentStatus Status { get; set; } = AcknowledgmentStatus.Pending;

        /// <summary>
        /// Cancellation token source for timeout handling
        /// </summary>
        public CancellationTokenSource? TimeoutTokenSource { get; set; }

        /// <summary>
        /// Error message if acknowledgment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Time when the acknowledgment was received
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>
        /// Completion source for awaiting acknowledgment
        /// </summary>
        public TaskCompletionSource<bool> CompletionSource { get; set; } = new();

        /// <summary>
        /// Gets the round-trip time for acknowledged messages
        /// </summary>
        public TimeSpan? RoundTripTime => AcknowledgedAt.HasValue ? AcknowledgedAt.Value - SentAt : null;

        /// <summary>
        /// Checks if the acknowledgment has timed out
        /// </summary>
        public bool IsTimedOut => DateTime.UtcNow > TimeoutAt && Status == AcknowledgmentStatus.Pending;
    }
}