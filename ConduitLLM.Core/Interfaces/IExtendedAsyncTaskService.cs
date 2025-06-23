using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Extended interface for async task service with additional functionality.
    /// </summary>
    public interface IExtendedAsyncTaskService : IAsyncTaskService
    {
        /// <summary>
        /// Creates a new async task with detailed parameters.
        /// </summary>
        /// <param name="taskType">The type of task.</param>
        /// <param name="payload">The task payload.</param>
        /// <param name="metadata">Additional metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created task.</returns>
        Task<AsyncTask> CreateTaskAsync(string taskType, object payload, Dictionary<string, object> metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a task status with detailed update information.
        /// </summary>
        /// <param name="taskId">The task ID to update.</param>
        /// <param name="update">The update details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated task status.</returns>
        Task<AsyncTaskStatus?> UpdateTaskStatusAsync(string taskId, AsyncTaskStatusUpdate update, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a running task.
        /// </summary>
        /// <param name="taskId">The task ID to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if cancelled successfully.</returns>
        Task<bool> CancelTaskAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a task.
        /// </summary>
        /// <param name="taskId">The task ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if deleted successfully.</returns>
        Task<bool> DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all tasks for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="activeOnly">Whether to return only active tasks.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of task statuses.</returns>
        Task<IList<AsyncTaskStatus>> GetTasksByVirtualKeyAsync(int virtualKeyId, bool activeOnly = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls for task completion.
        /// </summary>
        /// <param name="taskId">The task ID to poll.</param>
        /// <param name="pollInterval">How often to check.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The final task status.</returns>
        Task<AsyncTaskStatus?> PollForCompletionAsync(string taskId, TimeSpan pollInterval, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up old tasks.
        /// </summary>
        /// <param name="archiveAfter">Archive tasks older than this.</param>
        /// <param name="deleteAfter">Delete tasks older than this.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of archived and deleted count.</returns>
        Task<(int archived, int deleted)> CleanupOldTasksAsync(TimeSpan archiveAfter, TimeSpan deleteAfter, CancellationToken cancellationToken = default);
    }
}