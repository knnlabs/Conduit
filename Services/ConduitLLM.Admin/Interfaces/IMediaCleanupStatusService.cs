using ConduitLLM.Admin.DTOs;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service for tracking and reporting media cleanup status.
    /// </summary>
    public interface IMediaCleanupStatusService
    {
        /// <summary>
        /// Gets the current status of the media cleanup service.
        /// </summary>
        Task<MediaCleanupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the completion of a cleanup run.
        /// </summary>
        /// <param name="filesDeleted">Number of files deleted</param>
        /// <param name="bytesFreed">Bytes freed</param>
        /// <param name="durationSeconds">Duration of the run</param>
        /// <param name="status">Status message (e.g., "Completed", "Partial", "Failed")</param>
        /// <param name="leaderInstanceId">ID of the instance that ran the cleanup</param>
        /// <param name="triggeredBy">Whether the run was scheduled or manually triggered</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RecordRunCompletionAsync(
            int filesDeleted,
            long bytesFreed,
            double durationSeconds,
            string status,
            string leaderInstanceId,
            string triggeredBy,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the completion of one cleanup phase.
        /// </summary>
        Task RecordOperationCompletionAsync(
            string cleanupType,
            int filesDeleted,
            long bytesFreed,
            double durationSeconds,
            string status,
            string leaderInstanceId,
            string triggeredBy,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the untracked storage drift remaining after reconciliation.
        /// </summary>
        Task RecordReconciliationDriftAsync(
            int untrackedObjectCount,
            long untrackedBytes,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets whether the cleanup service is enabled via runtime toggle.
        /// </summary>
        Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the runtime enabled state of the cleanup service.
        /// </summary>
        Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the simple retention override value (days).
        /// Returns null if no override is set (using policy-based retention).
        /// </summary>
        Task<int?> GetSimpleRetentionOverrideAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets or clears the simple retention override.
        /// When set, all media is deleted after the specified number of days regardless of balance.
        /// Pass null to clear the override and use policy-based retention.
        /// </summary>
        /// <param name="days">Retention days (1-365), or null to clear</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetSimpleRetentionOverrideAsync(int? days, CancellationToken cancellationToken = default);
    }
}
