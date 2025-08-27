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
        /// Scheduler mode: "Disabled", "AdminApi", "CoreApi", "Any"
        /// </summary>
        public string SchedulerMode { get; set; } = "Disabled";

        /// <summary>
        /// Interval between scheduled cleanup runs in minutes.
        /// </summary>
        public int ScheduleIntervalMinutes { get; set; } = 60;

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
        /// Maximum batch size for R2 operations.
        /// Conservative for free tier.
        /// </summary>
        public int MaxBatchSize { get; set; } = 50;

        /// <summary>
        /// Delay between batches in milliseconds.
        /// </summary>
        public int DelayBetweenBatchesMs { get; set; } = 500;

        /// <summary>
        /// Maximum concurrent R2 delete operations.
        /// </summary>
        public int MaxConcurrentBatches { get; set; } = 2;

        /// <summary>
        /// Monthly delete operation budget for R2 free tier.
        /// </summary>
        public int MonthlyDeleteBudget { get; set; } = 500_000;

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
}