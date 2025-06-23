using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing async tasks.
    /// </summary>
    public interface IAsyncTaskRepository
    {
        /// <summary>
        /// Gets a task by its ID.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The task if found, null otherwise.</returns>
        Task<AsyncTask?> GetByIdAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all tasks for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of tasks for the virtual key.</returns>
        Task<List<AsyncTask>> GetByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets active (non-archived) tasks for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of active tasks for the virtual key.</returns>
        Task<List<AsyncTask>> GetActiveByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new async task.
        /// </summary>
        /// <param name="task">The task to create.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created task ID.</returns>
        Task<string> CreateAsync(AsyncTask task, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing async task.
        /// </summary>
        /// <param name="task">The task to update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if updated successfully, false otherwise.</returns>
        Task<bool> UpdateAsync(AsyncTask task, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a task by its ID.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        Task<bool> DeleteAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives completed tasks older than the specified timespan.
        /// </summary>
        /// <param name="olderThan">The age threshold for archiving.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of tasks archived.</returns>
        Task<int> ArchiveOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets tasks that need to be cleaned up (archived and older than specified timespan).
        /// </summary>
        /// <param name="archivedOlderThan">The age threshold for archived tasks.</param>
        /// <param name="limit">Maximum number of tasks to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of tasks to clean up.</returns>
        Task<List<AsyncTask>> GetTasksForCleanupAsync(TimeSpan archivedOlderThan, int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple tasks by their IDs.
        /// </summary>
        /// <param name="taskIds">The task IDs to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of tasks deleted.</returns>
        Task<int> BulkDeleteAsync(IEnumerable<string> taskIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all pending tasks that need to be processed.
        /// </summary>
        /// <param name="taskType">Optional filter by task type.</param>
        /// <param name="limit">Maximum number of tasks to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending tasks.</returns>
        Task<List<AsyncTask>> GetPendingTasksAsync(string? taskType = null, int limit = 100, CancellationToken cancellationToken = default);
    }
}