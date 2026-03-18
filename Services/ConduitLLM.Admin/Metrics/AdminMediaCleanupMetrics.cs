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
        /// Total cleanup cycles by status.
        /// </summary>
        public static readonly Counter CleanupCycles = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_cycles_total", "Total media cleanup cycles",
                new CounterConfiguration
                {
                    LabelNames = new[] { "status" } // status: completed, failed, cancelled, no_groups
                });

        /// <summary>
        /// Total files deleted during cleanup.
        /// </summary>
        public static readonly Counter FilesDeleted = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_files_deleted_total", "Total files deleted during cleanup");

        /// <summary>
        /// Total bytes freed during cleanup.
        /// </summary>
        public static readonly Counter BytesFreed = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_bytes_freed_total", "Total bytes freed during cleanup");

        /// <summary>
        /// Cleanup cycle duration in seconds.
        /// </summary>
        public static readonly Histogram CleanupDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_media_cleanup_duration_seconds", "Media cleanup cycle duration",
                new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(1, 2, 12) // 1s to ~4096s
                });

        /// <summary>
        /// Total cleanup errors.
        /// </summary>
        public static readonly Counter CleanupErrors = Prometheus.Metrics
            .CreateCounter("conduit_admin_media_cleanup_errors_total", "Total media cleanup errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "error_type" } // error_type: group_processing, batch_deletion, storage
                });

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
