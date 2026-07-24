namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Status information for the media cleanup service.
    /// Provides operational visibility into cleanup runs, budget usage, and configuration.
    /// </summary>
    public class MediaCleanupStatusDto
    {
        /// <summary>
        /// Whether the media cleanup service is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Whether the service is running in dry run mode (logs but doesn't delete).
        /// </summary>
        public bool IsDryRunMode { get; set; }

        /// <summary>
        /// The resolved media storage backend (for example, S3 or InMemory).
        /// </summary>
        public string StorageBackend { get; set; } = "Unavailable";

        /// <summary>
        /// Storage objects with no matching MediaRecord after the latest reconciliation.
        /// </summary>
        public int UntrackedObjectCount { get; set; }

        /// <summary>
        /// Bytes held by storage objects with no matching MediaRecord.
        /// </summary>
        public long UntrackedBytes { get; set; }

        /// <summary>
        /// The timestamp of the last cleanup run (UTC).
        /// Null if no cleanup has run yet.
        /// </summary>
        public DateTime? LastRunTimeUtc { get; set; }

        /// <summary>
        /// The result of the last cleanup run.
        /// </summary>
        public string? LastRunStatus { get; set; }

        /// <summary>
        /// Source of the last run, such as scheduled or manual.
        /// </summary>
        public string? LastRunTriggeredBy { get; set; }

        /// <summary>
        /// Number of files deleted in the last run.
        /// </summary>
        public int LastRunFilesDeleted { get; set; }

        /// <summary>
        /// Bytes freed in the last run.
        /// </summary>
        public long LastRunBytesFreed { get; set; }

        /// <summary>
        /// Duration of the last cleanup run in seconds.
        /// </summary>
        public double? LastRunDurationSeconds { get; set; }

        /// <summary>
        /// Number of delete operations performed this month.
        /// </summary>
        public long MonthlyDeleteCount { get; set; }

        /// <summary>
        /// Monthly delete budget limit.
        /// </summary>
        public int MonthlyDeleteBudget { get; set; }

        /// <summary>
        /// Remaining delete operations available this month.
        /// </summary>
        public long MonthlyDeleteBudgetRemaining { get; set; }

        /// <summary>
        /// Percentage of monthly budget used (0-100).
        /// </summary>
        public double MonthlyBudgetUsedPercent { get; set; }

        /// <summary>
        /// Interval between cleanup runs in minutes.
        /// </summary>
        public int ScheduleIntervalMinutes { get; set; }

        /// <summary>
        /// Maximum batch size for deletions.
        /// </summary>
        public int MaxBatchSize { get; set; }

        /// <summary>
        /// Summary of the default retention policy.
        /// </summary>
        public RetentionPolicySummaryDto? DefaultRetentionPolicy { get; set; }

        /// <summary>
        /// Total number of active retention policies.
        /// </summary>
        public int ActiveRetentionPoliciesCount { get; set; }

        /// <summary>
        /// Estimated next run time (UTC).
        /// </summary>
        public DateTime? NextScheduledRunUtc { get; set; }

        /// <summary>
        /// Instance ID of the current cleanup leader (if known).
        /// </summary>
        public string? CurrentLeaderInstanceId { get; set; }

        /// <summary>
        /// Last known outcome for each cleanup phase owned by the scheduler.
        /// </summary>
        public List<MediaCleanupOperationStatusDto> OperationStatuses { get; set; } = new();

        /// <summary>
        /// Simple retention override in days.
        /// When set, all media is deleted after this many days regardless of account balance.
        /// Null means using policy-based retention.
        /// </summary>
        public int? SimpleRetentionOverrideDays { get; set; }

        /// <summary>
        /// Whether the simple retention override is active.
        /// </summary>
        public bool IsSimpleRetentionOverrideActive => SimpleRetentionOverrideDays.HasValue;
    }

    /// <summary>
    /// Last known status for one scheduled media cleanup phase.
    /// </summary>
    public class MediaCleanupOperationStatusDto
    {
        /// <summary>
        /// Stable cleanup phase name: expiration, reconciliation, or retention.
        /// </summary>
        public string CleanupType { get; set; } = string.Empty;

        /// <summary>
        /// Whether this cleanup phase is enabled by deploy-time configuration.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Timestamp of the last run for this phase (UTC).
        /// </summary>
        public DateTime? LastRunTimeUtc { get; set; }

        /// <summary>
        /// Outcome of the last run for this phase.
        /// </summary>
        public string? LastRunStatus { get; set; }

        /// <summary>
        /// Source that triggered the last run of this phase.
        /// </summary>
        public string? TriggeredBy { get; set; }

        /// <summary>
        /// Number of files deleted by the last run of this phase.
        /// </summary>
        public int LastRunFilesDeleted { get; set; }

        /// <summary>
        /// Bytes freed by the last run of this phase.
        /// </summary>
        public long LastRunBytesFreed { get; set; }

        /// <summary>
        /// Duration of the last run of this phase in seconds.
        /// </summary>
        public double? LastRunDurationSeconds { get; set; }
    }

    /// <summary>
    /// Stable names for the cleanup phases owned by the media lifecycle scheduler.
    /// </summary>
    public static class MediaCleanupTypes
    {
        public const string Expiration = "expiration";
        public const string Reconciliation = "reconciliation";
        public const string Retention = "retention";
        public const string VirtualKey = "virtual-key";

        public static readonly IReadOnlyList<string> All =
            new[] { Expiration, Reconciliation, Retention };
    }

    /// <summary>
    /// Summary of a retention policy for display.
    /// </summary>
    public class RetentionPolicySummaryDto
    {
        /// <summary>
        /// Name of the policy.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Retention days for positive balance accounts.
        /// </summary>
        public int PositiveBalanceRetentionDays { get; set; }

        /// <summary>
        /// Retention days for zero balance accounts.
        /// </summary>
        public int ZeroBalanceRetentionDays { get; set; }

        /// <summary>
        /// Retention days for negative balance accounts.
        /// </summary>
        public int NegativeBalanceRetentionDays { get; set; }
    }

    /// <summary>
    /// Request to update the media cleanup enabled state.
    /// </summary>
    public class UpdateMediaCleanupEnabledRequest
    {
        /// <summary>
        /// Whether to enable or disable the media cleanup service.
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Request to set or clear the simple retention override.
    /// </summary>
    public class UpdateSimpleRetentionRequest
    {
        /// <summary>
        /// Retention days (1-365), or null to clear the override and use policy-based retention.
        /// </summary>
        public int? RetentionDays { get; set; }
    }

    /// <summary>
    /// Response for simple retention override operations.
    /// </summary>
    public class SimpleRetentionResponse
    {
        /// <summary>
        /// Current retention days setting (null if using policy-based).
        /// </summary>
        public int? RetentionDays { get; set; }

        /// <summary>
        /// Whether the simple override is currently active.
        /// </summary>
        public bool IsOverrideActive { get; set; }

        /// <summary>
        /// Informational message about the change.
        /// </summary>
        public string? Message { get; set; }
    }
}
