using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for managing the distributed image generation task queue.
    /// Supports multiple service instances processing tasks concurrently.
    /// </summary>
    public interface IImageGenerationQueue
    {
        /// <summary>
        /// Enqueues an image generation request for async processing.
        /// </summary>
        /// <param name="request">The image generation request event.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The unique task ID for tracking.</returns>
        Task<string> EnqueueAsync(ImageGenerationRequested request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to dequeue and claim the next available task.
        /// Only one instance can successfully claim a task.
        /// </summary>
        /// <param name="instanceId">The unique identifier of the processing instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The claimed task, or null if no tasks are available.</returns>
        Task<ImageGenerationRequested?> DequeueAsync(string instanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Acknowledges successful processing of a task.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AcknowledgeAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a task to the queue for retry after a failure.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="error">The error that occurred.</param>
        /// <param name="retryAfter">Optional delay before the task can be retried.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReturnToQueueAsync(string taskId, string error, TimeSpan? retryAfter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current queue depth (number of pending tasks).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of tasks waiting to be processed.</returns>
        Task<long> GetQueueDepthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of tasks currently being processed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of active tasks.</returns>
        Task<long> GetActiveTaskCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Extends the claim on a task to prevent timeout.
        /// Should be called periodically during long-running generations.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="instanceId">The instance that owns the task.</param>
        /// <param name="extension">The time to extend the claim.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the claim was extended successfully.</returns>
        Task<bool> ExtendClaimAsync(string taskId, string instanceId, TimeSpan extension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Recovers orphaned tasks that have exceeded their claim timeout.
        /// </summary>
        /// <param name="claimTimeout">The timeout after which a task is considered orphaned.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of tasks recovered.</returns>
        Task<int> RecoverOrphanedTasksAsync(TimeSpan claimTimeout, CancellationToken cancellationToken = default);
    }
}