using System.Collections.Generic;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Message for task progress updates that require acknowledgment
    /// </summary>
    public class TaskProgressMessage : SignalRMessage
    {
        public override string MessageType => "TaskProgress";

        /// <summary>
        /// ID of the task
        /// </summary>
        public string TaskId { get; set; } = null!;

        /// <summary>
        /// Current progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Human-readable status message
        /// </summary>
        public string StatusMessage { get; set; } = null!;

        /// <summary>
        /// Additional metadata about the progress
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Estimated time remaining in seconds
        /// </summary>
        public int? EstimatedSecondsRemaining { get; set; }

        public TaskProgressMessage()
        {
            IsCritical = true; // Task progress updates are critical
            Priority = 5; // Medium priority
        }
    }
}