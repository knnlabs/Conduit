using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing async tasks.
    /// Extends IRepositoryBase for standard CRUD operations.
    /// </summary>
    public interface IAsyncTaskRepository : IRepositoryBase<AsyncTask, string>
    {
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
        /// Archives completed tasks older than the specified timespan.
        /// </summary>
        /// <param name="olderThan">The age threshold for archiving.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of tasks archived.</returns>
        Task<int> ArchiveOldTasksAsync(
            TimeSpan completedOlderThan,
            TimeSpan? staleActiveOlderThan = null,
            CancellationToken cancellationToken = default);

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

        /// <summary>Atomically claims a specific pending task for one media consumer.</summary>
        Task<AsyncTaskClaimResult> TryClaimTaskAsync(
            string taskId,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default);

        /// <summary>Records that the external provider may now have accepted work.</summary>
        Task<bool> MarkProviderInvocationStartedAsync(
            string taskId,
            string workerId,
            CancellationToken cancellationToken = default);

        /// <summary>Records that the provider returned a definitive result.</summary>
        Task<bool> MarkProviderInvocationCompletedAsync(
            string taskId,
            string workerId,
            string? providerOperationId = null,
            CancellationToken cancellationToken = default);

        Task<bool> ResolveIndeterminateTaskAsync(
            string taskId,
            int targetState,
            bool isRetryable,
            string reason,
            string? providerOperationId = null,
            CancellationToken cancellationToken = default);

        Task<ExpiredTaskRecoveryResult> RecoverExpiredMediaTasksAsync(
            CancellationToken cancellationToken = default);
    }

    public enum AsyncTaskClaimResult
    {
        Claimed = 0,
        AlreadyClaimed = 1,
        Terminal = 2,
        Missing = 3,
        Indeterminate = 4
    }

    public sealed record ExpiredTaskRecoveryResult(int ResetToPending, int MarkedIndeterminate);
}
