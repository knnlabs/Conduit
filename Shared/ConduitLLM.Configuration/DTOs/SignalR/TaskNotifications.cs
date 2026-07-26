namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Status update for async tasks sent via SignalR.
    /// </summary>
    public class AsyncTaskStatusUpdate
    {
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the task status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        public double? Progress { get; set; }

        /// <summary>
        /// Gets or sets the current step or phase.
        /// </summary>
        public string? CurrentStep { get; set; }

        /// <summary>
        /// Gets or sets a message describing the current state.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the result data if completed.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Gets or sets the error information if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets when the task started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the task completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the estimated time remaining in seconds.
        /// </summary>
        public double? EstimatedSecondsRemaining { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of this update.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
