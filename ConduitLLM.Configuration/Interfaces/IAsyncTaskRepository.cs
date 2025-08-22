using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
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

        /// <summary>
        /// Attempts to lease a pending task for processing.
        /// </summary>
        /// <param name="workerId">The ID of the worker attempting to lease the task.</param>
        /// <param name="leaseDuration">How long to lease the task for.</param>
        /// <param name="taskType">Optional filter by task type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The leased task if successful, null otherwise.</returns>
        Task<AsyncTask?> LeaseNextPendingTaskAsync(string workerId, TimeSpan leaseDuration, string? taskType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases a task lease.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <param name="workerId">The worker ID that holds the lease.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the lease was released, false otherwise.</returns>
        Task<bool> ReleaseLeaseAsync(string taskId, string workerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extends the lease on a task.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <param name="workerId">The worker ID that holds the lease.</param>
        /// <param name="extension">How long to extend the lease.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the lease was extended, false otherwise.</returns>
        Task<bool> ExtendLeaseAsync(string taskId, string workerId, TimeSpan extension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets tasks with expired leases.
        /// </summary>
        /// <param name="limit">Maximum number of tasks to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of tasks with expired leases.</returns>
        Task<List<AsyncTask>> GetExpiredLeaseTasksAsync(int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a task with optimistic concurrency control.
        /// </summary>
        /// <param name="task">The task to update.</param>
        /// <param name="expectedVersion">The expected version number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if updated successfully, false if version mismatch.</returns>
        Task<bool> UpdateWithVersionCheckAsync(AsyncTask task, int expectedVersion, CancellationToken cancellationToken = default);
    }
}