using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing asynchronous tasks that may take a long time to complete.
    /// This is used for operations like image/video generation that require polling for results.
    /// </summary>
    public interface IAsyncTaskService
    {
        /// <summary>
        /// Creates a new async task and returns a task ID for tracking.
        /// </summary>
        /// <param name="taskType">The type of task (e.g., "image_generation", "video_generation")</param>
        /// <param name="metadata">Additional metadata about the task</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A unique task ID for tracking the task</returns>
        Task<string> CreateTaskAsync(string taskType, object metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new async task with explicit virtual key ID and returns a task ID for tracking.
        /// </summary>
        /// <param name="taskType">The type of task (e.g., "image_generation", "video_generation")</param>
        /// <param name="virtualKeyId">The virtual key ID associated with this task</param>
        /// <param name="metadata">Additional metadata about the task</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A unique task ID for tracking the task</returns>
        Task<string> CreateTaskAsync(string taskType, int virtualKeyId, object metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of a task.
        /// </summary>
        /// <param name="taskId">The ID of the task to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The current task status</returns>
        Task<AsyncTaskStatus?> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the status of a task.
        /// </summary>
        /// <param name="taskId">The ID of the task to update</param>
        /// <param name="status">The new status</param>
        /// <param name="result">Optional result data</param>
        /// <param name="error">Optional error message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpdateTaskStatusAsync(string taskId, TaskState status, int? progress = null, object? result = null, string? error = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls a task until it completes, fails, or times out.
        /// </summary>
        /// <param name="taskId">The ID of the task to poll</param>
        /// <param name="pollingInterval">How often to check the status</param>
        /// <param name="timeout">Maximum time to wait for completion</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The final task status with result</returns>
        Task<AsyncTaskStatus> PollTaskUntilCompletedAsync(
            string taskId, 
            TimeSpan pollingInterval, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a running task.
        /// </summary>
        /// <param name="taskId">The ID of the task to cancel</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a task and its associated data.
        /// </summary>
        /// <param name="taskId">The ID of the task to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up old completed or failed tasks.
        /// </summary>
        /// <param name="olderThan">Remove tasks older than this timespan</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of tasks cleaned up</returns>
        Task<int> CleanupOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the status of an async task.
    /// </summary>
    public class AsyncTaskStatus
    {
        /// <summary>
        /// Unique identifier for the task.
        /// </summary>
        public string TaskId { get; set; } = "";

        /// <summary>
        /// The type of task (e.g., "image_generation", "video_generation").
        /// </summary>
        public string TaskType { get; set; } = "";

        /// <summary>
        /// Current state of the task.
        /// </summary>
        public TaskState State { get; set; }

        /// <summary>
        /// When the task was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the task was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// When the task completed (if applicable).
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// The result of the task (if completed successfully).
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Error message if the task failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Additional metadata about the task.
        /// </summary>
        public object? Metadata { get; set; }

        /// <summary>
        /// Progress percentage (0-100) if available.
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// Progress message if available.
        /// </summary>
        public string? ProgressMessage { get; set; }
    }

    /// <summary>
    /// Possible states for an async task.
    /// </summary>
    public enum TaskState
    {
        /// <summary>
        /// Task has been created but not started.
        /// </summary>
        Pending,

        /// <summary>
        /// Task is currently being processed.
        /// </summary>
        Processing,

        /// <summary>
        /// Task completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Task failed with an error.
        /// </summary>
        Failed,

        /// <summary>
        /// Task was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Task timed out.
        /// </summary>
        TimedOut
    }
}