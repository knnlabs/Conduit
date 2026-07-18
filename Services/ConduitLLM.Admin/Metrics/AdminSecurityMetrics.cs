using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for Admin API security middleware.
    /// Tracks authentication failures, rate limit hits, and access denials.
    /// </summary>
    public static class AdminSecurityMetrics
    {
        /// <summary>
        /// Total security violations by type.
        /// </summary>
        public static readonly Counter SecurityViolations = Prometheus.Metrics
            .CreateCounter("conduit_admin_security_violations_total", "Total security violations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "type" } // type: auth_failure, rate_limit, access_denied, blocked
                });

        /// <summary>
        /// Total requests allowed through security middleware.
        /// </summary>
        public static readonly Counter RequestsAllowed = Prometheus.Metrics
            .CreateCounter("conduit_admin_security_requests_allowed_total", "Total requests allowed through security");

        // Convenience methods

        /// <summary>Records an authentication failure (401).</summary>
        public static void RecordAuthFailure()
            => SecurityViolations.WithLabels("auth_failure").Inc();

        /// <summary>Records a rate limit hit (429).</summary>
        public static void RecordRateLimitHit()
            => SecurityViolations.WithLabels("rate_limit").Inc();

        /// <summary>Records an access denial (403).</summary>
        public static void RecordAccessDenied()
            => SecurityViolations.WithLabels("access_denied").Inc();

        /// <summary>Records an unspecified security block.</summary>
        public static void RecordBlocked()
            => SecurityViolations.WithLabels("blocked").Inc();
    }
}
