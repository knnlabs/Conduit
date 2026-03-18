using Prometheus;

namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// Prometheus metrics for Gateway authentication operations.
    /// Tracks success/failure rates and latency across all authentication schemes.
    /// </summary>
    public static class GatewayAuthMetrics
    {
        /// <summary>
        /// Total authentication attempts by scheme and result.
        /// </summary>
        public static readonly Counter AuthAttempts = Prometheus.Metrics
            .CreateCounter("conduit_gateway_auth_attempts_total", "Total authentication attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "scheme", "result" } // scheme: VirtualKey, EphemeralKey, Backend; result: success, failure, no_result, error
                });

        /// <summary>
        /// Authentication duration by scheme.
        /// </summary>
        public static readonly Histogram AuthDuration = Prometheus.Metrics
            .CreateHistogram("conduit_gateway_auth_duration_seconds", "Authentication duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "scheme" },
                    Buckets = Histogram.ExponentialBuckets(0.0001, 2, 14) // 0.1ms to ~1.6s
                });

        /// <summary>
        /// Authentication failures by scheme and reason.
        /// </summary>
        public static readonly Counter AuthFailures = Prometheus.Metrics
            .CreateCounter("conduit_gateway_auth_failures_total", "Authentication failures by reason",
                new CounterConfiguration
                {
                    LabelNames = new[] { "scheme", "reason" } // reason: invalid_key, expired, disabled, not_found, error
                });

        // Convenience methods

        public static void RecordSuccess(string scheme)
            => AuthAttempts.WithLabels(scheme, "success").Inc();

        public static void RecordFailure(string scheme, string reason)
        {
            AuthAttempts.WithLabels(scheme, "failure").Inc();
            AuthFailures.WithLabels(scheme, reason).Inc();
        }

        public static void RecordNoResult(string scheme)
            => AuthAttempts.WithLabels(scheme, "no_result").Inc();

        public static void RecordError(string scheme)
        {
            AuthAttempts.WithLabels(scheme, "error").Inc();
            AuthFailures.WithLabels(scheme, "error").Inc();
        }

        public static void RecordDuration(string scheme, double durationSeconds)
            => AuthDuration.WithLabels(scheme).Observe(durationSeconds);
    }
}
