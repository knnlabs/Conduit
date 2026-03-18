using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for Admin API authentication operations.
    /// Tracks success/failure rates and latency across all authentication schemes.
    /// </summary>
    public static class AdminAuthMetrics
    {
        /// <summary>
        /// Total authentication attempts by scheme and result.
        /// </summary>
        public static readonly Counter AuthAttempts = Prometheus.Metrics
            .CreateCounter("conduit_admin_auth_attempts_total", "Total authentication attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "scheme", "result" } // scheme: MasterKey, EphemeralKey, HealthCheck; result: success, failure
                });

        /// <summary>
        /// Authentication duration by scheme.
        /// </summary>
        public static readonly Histogram AuthDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_auth_duration_seconds", "Authentication duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "scheme" },
                    Buckets = Histogram.ExponentialBuckets(0.0001, 2, 14) // 0.1ms to ~1.6s
                });

        /// <summary>
        /// Authentication failures by scheme and reason.
        /// </summary>
        public static readonly Counter AuthFailures = Prometheus.Metrics
            .CreateCounter("conduit_admin_auth_failures_total", "Authentication failures by reason",
                new CounterConfiguration
                {
                    LabelNames = new[] { "scheme", "reason" } // reason: missing_key, invalid_key, not_configured, expired, not_found, already_used, error
                });

        /// <summary>Records a successful authentication attempt.</summary>
        /// <param name="scheme">The authentication scheme (e.g., "MasterKey", "EphemeralKey", "HealthCheck").</param>
        public static void RecordSuccess(string scheme)
            => AuthAttempts.WithLabels(scheme, "success").Inc();

        /// <summary>Records a failed authentication attempt, incrementing both the attempt and failure counters.</summary>
        /// <param name="scheme">The authentication scheme (e.g., "MasterKey", "EphemeralKey", "HealthCheck").</param>
        /// <param name="reason">The failure reason (e.g., "missing_key", "invalid_key", "expired").</param>
        public static void RecordFailure(string scheme, string reason)
        {
            AuthAttempts.WithLabels(scheme, "failure").Inc();
            AuthFailures.WithLabels(scheme, reason).Inc();
        }

        /// <summary>Records the duration of an authentication operation.</summary>
        /// <param name="scheme">The authentication scheme.</param>
        /// <param name="durationSeconds">The authentication duration in seconds.</param>
        public static void RecordDuration(string scheme, double durationSeconds)
            => AuthDuration.WithLabels(scheme).Observe(durationSeconds);
    }
}
