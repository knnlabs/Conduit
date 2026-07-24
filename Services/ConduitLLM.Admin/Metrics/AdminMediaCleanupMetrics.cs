using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for media cleanup operations.
    /// Tracks files deleted, bytes freed, duration, and errors.
    /// </summary>
    public static class AdminMediaCleanupMetrics
    {
        /// <summary>
        /// Total cleanup phase executions by type and status.
        /// </summary>
        public static readonly Counter CleanupCycles = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_cycles_total", "Total media cleanup cycles",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cleanup_type", "status" }
                });

        /// <summary>
        /// Total cleanup runs split by operation trigger and outcome.
        /// </summary>
        public static readonly Counter CleanupRuns = Prometheus.Metrics
            .CreateCounter(
                "conduit_admin_media_cleanup_runs_total",
                "Total media cleanup runs by trigger",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cleanup_type", "triggered_by", "status" }
                });

        /// <summary>
        /// Storage objects with no matching MediaRecord after the latest reconciliation.
        /// </summary>
        public static readonly Gauge UntrackedObjects = Prometheus.Metrics
            .CreateGauge(
                "conduit_admin_media_cleanup_untracked_objects",
                "Storage objects with no matching MediaRecord");

        /// <summary>
        /// Bytes held by storage objects with no matching MediaRecord.
        /// </summary>
        public static readonly Gauge UntrackedBytes = Prometheus.Metrics
            .CreateGauge(
                "conduit_admin_media_cleanup_untracked_bytes",
                "Bytes held by storage objects with no matching MediaRecord");

        /// <summary>
        /// Large cleanup scopes currently awaiting administrator approval.
        /// </summary>
        public static readonly Gauge PendingApprovals = Prometheus.Metrics
            .CreateGauge(
                "conduit_admin_media_cleanup_pending_approvals",
                "Large media cleanup scopes awaiting administrator approval");

        /// <summary>
        /// Total files deleted during cleanup.
        /// </summary>
        public static readonly Counter FilesDeleted = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_files_deleted_total", "Total files deleted during cleanup",
                new CounterConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Total bytes freed during cleanup.
        /// </summary>
        public static readonly Counter BytesFreed = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_bytes_freed_total", "Total bytes freed during cleanup",
                new CounterConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Files that a dry run determined would be permanently deleted.
        /// </summary>
        public static readonly Counter DryRunFilesMatched = Prometheus.Metrics
            .CreateCounter(
                "conduit_admin_media_cleanup_dry_run_files_total",
                "Files that media cleanup would delete while in dry-run mode",
                new CounterConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Bytes that a dry run determined would be freed.
        /// </summary>
        public static readonly Counter DryRunBytesMatched = Prometheus.Metrics
            .CreateCounter(
                "conduit_admin_media_cleanup_dry_run_bytes_total",
                "Bytes that media cleanup would free while in dry-run mode",
                new CounterConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Records that a dry run determined would be tombstoned.
        /// </summary>
        public static readonly Counter DryRunRecordsMatched = Prometheus.Metrics
            .CreateCounter(
                "conduit_admin_media_cleanup_dry_run_records_total",
                "Records that media cleanup would tombstone while in dry-run mode",
                new CounterConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Total media records tombstoned. Tombstoning does not free storage or consume delete budget.
        /// </summary>
        public static readonly Counter RecordsTombstoned = Prometheus.Metrics
            .CreateCounter(
                "conduit_admin_media_cleanup_records_tombstoned_total",
                "Total media records tombstoned during cleanup",
                new CounterConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Cleanup cycle duration in seconds.
        /// </summary>
        public static readonly Histogram CleanupDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_media_cleanup_duration_seconds", "Media cleanup cycle duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "cleanup_type" },
                    Buckets = Histogram.ExponentialBuckets(1, 2, 12) // 1s to ~4096s
                });

        /// <summary>
        /// Total cleanup errors.
        /// </summary>
        public static readonly Counter CleanupErrors = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_errors_total", "Total media cleanup errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cleanup_type", "error_type" }
                });

        /// <summary>
        /// Unix timestamp of the last completed run for each cleanup phase.
        /// </summary>
        public static readonly Gauge LastRunTimestamp = Prometheus.Metrics
            .CreateGauge("conduit_admin_media_cleanup_last_run_timestamp_seconds",
                "Unix timestamp of the last completed media cleanup phase",
                new GaugeConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Whether the last phase run completed without errors (1) or not (0).
        /// </summary>
        public static readonly Gauge LastRunSucceeded = Prometheus.Metrics
            .CreateGauge("conduit_admin_media_cleanup_last_run_succeeded",
                "Whether the last media cleanup phase completed without errors",
                new GaugeConfiguration { LabelNames = new[] { "cleanup_type" } });

        /// <summary>
        /// Number of virtual key groups processed per cleanup cycle.
        /// </summary>
        public static readonly Histogram GroupsProcessed = Prometheus.Metrics
            .CreateHistogram("conduit_admin_media_cleanup_groups_processed", "Groups processed per cleanup cycle",
                new HistogramConfiguration
                {
                    Buckets = Histogram.LinearBuckets(0, 5, 20) // 0 to 100 in steps of 5
                });
    }
}
