namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for media lifecycle management.
    /// </summary>
    public class MediaLifecycleOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "MediaLifecycle";

        /// <summary>
        /// Whether to run in dry-run mode (no actual deletions).
        /// Default: true for safety.
        /// </summary>
        public bool DryRunMode { get; set; } = true;

        /// <summary>
        /// Enable the media cleanup scheduler.
        /// Default: false for safety (must be explicitly enabled).
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Determines if the scheduler should run.
        /// </summary>
        public bool IsSchedulerEnabled => Enabled;

        /// <summary>
        /// Interval between scheduled cleanup runs in minutes.
        /// </summary>
        public int ScheduleIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Enable cleanup of media whose explicit expiration time has passed.
        /// </summary>
        public bool EnableExpirationCleanup { get; set; } = true;

        /// <summary>
        /// Enable storage reconciliation for objects that have no MediaRecord.
        /// </summary>
        public bool EnableReconciliation { get; set; } = true;

        /// <summary>
        /// Minimum age in hours before an untracked storage object may be deleted.
        /// Protects uploads that have not finished writing their MediaRecord.
        /// </summary>
        public int ReconciliationMinimumAgeHours { get; set; } = 48;

        /// <summary>
        /// Number of storage objects inspected per reconciliation page.
        /// </summary>
        public int ReconciliationPageSize { get; set; } = 1000;

        /// <summary>
        /// Enable cleanup based on virtual key group retention policies.
        /// </summary>
        public bool EnableRetentionCleanup { get; set; } = true;

        /// <summary>
        /// Enable permanent eviction when virtual-key groups exceed policy storage quotas.
        /// </summary>
        public bool EnableQuotaCleanup { get; set; } = true;

        /// <summary>
        /// Enable soft delete with grace period before permanent deletion.
        /// </summary>
        public bool EnableSoftDelete { get; set; } = true;

        /// <summary>
        /// Grace period in days for soft-deleted media.
        /// </summary>
        public int SoftDeleteGracePeriodDays { get; set; } = 7;

        /// <summary>
        /// Test virtual key group IDs for progressive rollout.
        /// Only these groups will be processed when not empty.
        /// </summary>
        public List<int> TestVirtualKeyGroups { get; set; } = new();

        /// <summary>
        /// Require manual approval for batches larger than this threshold.
        /// </summary>
        public bool RequireManualApprovalForLargeBatches { get; set; } = false;

        /// <summary>
        /// Threshold for considering a batch "large".
        /// </summary>
        public int LargeBatchThreshold { get; set; } = 100;

        /// <summary>
        /// Number of hours before a pending or unused approval must be re-evaluated.
        /// </summary>
        public int LargeBatchApprovalExpirationHours { get; set; } = 24;

        /// <summary>
        /// Maximum number of objects grouped into one cleanup batch.
        /// S3-compatible bulk delete supports up to 1,000 keys.
        /// </summary>
        public int MaxBatchSize { get; set; } = 1000;

        /// <summary>
        /// Delay between batches in milliseconds.
        /// </summary>
        public int DelayBetweenBatchesMs { get; set; } = 500;

        /// <summary>
        /// Maximum retries after a storage throttle or per-call timeout.
        /// </summary>
        public int DeleteThrottleMaxRetries { get; set; } = 5;

        /// <summary>
        /// Initial exponential backoff delay after storage throttling.
        /// </summary>
        public int DeleteThrottleInitialBackoffMs { get; set; } = 1000;

        /// <summary>
        /// Monthly delete operation budget for R2 free tier.
        /// </summary>
        public int MonthlyDeleteBudget { get; set; } = 500_000;

        /// <summary>
        /// Maximum number of permanent deletes reserved before each storage sub-chunk.
        /// Smaller values reduce the accounting exposure if a process crashes after reservation.
        /// </summary>
        public int BudgetReservationStride { get; set; } = 10;

        /// <summary>
        /// Behavior when the shared budget store cannot be read or updated.
        /// </summary>
        public MediaBudgetFailureMode BudgetFailureMode { get; set; } =
            MediaBudgetFailureMode.FailClosed;

        /// <summary>
        /// Enable detailed audit logging for all media deletions.
        /// </summary>
        public bool EnableAuditLogging { get; set; } = true;

        /// <summary>
        /// Enable metrics collection for monitoring.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Timeout for R2 operations in seconds.
        /// </summary>
        public int R2OperationTimeoutSeconds { get; set; } = 30;
    }

    public enum MediaBudgetFailureMode
    {
        FailClosed,
        FailOpen
    }
}
