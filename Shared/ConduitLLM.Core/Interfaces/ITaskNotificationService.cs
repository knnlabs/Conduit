using Polly.CircuitBreaker;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for sending task notifications through SignalR hubs.
    /// This service can be injected into any component that needs to send real-time task updates.
    /// Includes retry logic and circuit breaker for resilient communication.
    /// </summary>
    public interface ITaskNotificationService
    {
        /// <summary>
        /// Notifies that a task has started.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="taskType">Type of task (e.g., "image_generation", "video_generation", "spend_update")</param>
        /// <param name="virtualKeyId">Virtual key ID that owns this task</param>
        /// <param name="metadata">Additional metadata about the task</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyTaskStartedAsync(string taskId, string taskType, int virtualKeyId, object? metadata = null);

        /// <summary>
        /// Sends a progress update for a task.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="progress">Progress percentage (0-100)</param>
        /// <param name="message">Optional progress message</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyTaskProgressAsync(string taskId, int progress, string? message = null);

        /// <summary>
        /// Notifies that a task has completed successfully.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="result">Result of the completed task</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyTaskCompletedAsync(string taskId, object? result = null);

        /// <summary>
        /// Notifies that a task has failed.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="error">Error message describing the failure</param>
        /// <param name="isRetryable">Whether the task can be retried</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyTaskFailedAsync(string taskId, string error, bool isRetryable = false);

        /// <summary>
        /// Notifies that a task has been cancelled.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="reason">Optional reason for cancellation</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyTaskCancelledAsync(string taskId, string? reason = null);

        /// <summary>
        /// Notifies that a task has timed out.
        /// </summary>
        /// <param name="taskId">Unique identifier for the task</param>
        /// <param name="timeoutSeconds">Number of seconds before timeout occurred</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyTaskTimedOutAsync(string taskId, int timeoutSeconds);

        /// <summary>
        /// Gets the current circuit breaker state for monitoring purposes.
        /// </summary>
        /// <returns>The current circuit breaker state</returns>
        CircuitState GetCircuitState();

        /// <summary>
        /// Manually reset the circuit breaker if needed.
        /// </summary>
        void ResetCircuitBreaker();

        /// <summary>
        /// Get health status of the notification service.
        /// </summary>
        /// <returns>Tuple containing health status, state description, and last failure time</returns>
        (bool IsHealthy, string State, DateTime? LastFailure) GetHealthStatus();
    }
}