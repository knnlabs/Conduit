using System.Collections.Generic;

namespace ConduitLLM.Http.SignalR.Messages
{
    /// <summary>
    /// Message for task completion notifications that require acknowledgment
    /// </summary>
    public class TaskCompletedMessage : SignalRMessage
    {
        public override string MessageType => "TaskCompleted";

        /// <summary>
        /// ID of the completed task
        /// </summary>
        public string TaskId { get; set; } = null!;

        /// <summary>
        /// Indicates if the task completed successfully
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Result data from the task
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Error message if the task failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code if the task failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Total duration of the task in milliseconds
        /// </summary>
        public long DurationMilliseconds { get; set; }

        /// <summary>
        /// Additional metadata about the completion
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        public TaskCompletedMessage()
        {
            IsCritical = true; // Task completion notifications are critical
            Priority = 10; // High priority
        }
    }
}