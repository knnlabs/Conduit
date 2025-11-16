using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents an update to an async task status.
    /// </summary>
    public class AsyncTaskStatusUpdate
    {
        /// <summary>
        /// The new state for the task.
        /// </summary>
        public TaskState? State { get; set; }

        /// <summary>
        /// The progress percentage (0-100).
        /// </summary>
        public int? Progress { get; set; }

        /// <summary>
        /// Progress message to display.
        /// </summary>
        public string? ProgressMessage { get; set; }

        /// <summary>
        /// The result data if task is completed.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Error message if task failed.
        /// </summary>
        public string? Error { get; set; }
    }
}