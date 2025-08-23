namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for unified task tracking across all async operations in Conduit.
    /// Provides standard lifecycle methods for task started, progress, completed, and failed events.
    /// </summary>
    public interface ITaskHub
    {
        /// <summary>
        /// Notifies clients that a task has started.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="taskType">Type of task (e.g., "image_generation", "video_generation", "spend_update")</param>
        /// <param name="metadata">Additional metadata about the task</param>
        /// <returns>Task representing the async operation</returns>
        Task TaskStarted(string taskId, string taskType, object metadata);

        /// <summary>
        /// Updates clients on task progress.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="progress">Progress percentage (0-100)</param>
        /// <param name="message">Optional progress message</param>
        /// <returns>Task representing the async operation</returns>
        Task TaskProgress(string taskId, int progress, string? message = null);

        /// <summary>
        /// Notifies clients that a task has completed successfully.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="result">Result of the completed task</param>
        /// <returns>Task representing the async operation</returns>
        Task TaskCompleted(string taskId, object result);

        /// <summary>
        /// Notifies clients that a task has failed.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="error">Error message describing the failure</param>
        /// <param name="isRetryable">Whether the task can be retried</param>
        /// <returns>Task representing the async operation</returns>
        Task TaskFailed(string taskId, string error, bool isRetryable = false);

        /// <summary>
        /// Notifies clients that a task has been cancelled.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="reason">Optional reason for cancellation</param>
        /// <returns>Task representing the async operation</returns>
        Task TaskCancelled(string taskId, string? reason = null);

        /// <summary>
        /// Notifies clients that a task has timed out.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="timeoutSeconds">Number of seconds before timeout occurred</param>
        /// <returns>Task representing the async operation</returns>
        Task TaskTimedOut(string taskId, int timeoutSeconds);
    }
}