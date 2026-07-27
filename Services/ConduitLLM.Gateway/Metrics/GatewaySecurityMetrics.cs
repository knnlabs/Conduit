using Prometheus;

namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// Prometheus metrics for Gateway API security middleware.
    /// </summary>
    public static class GatewaySecurityMetrics
    {
        /// <summary>
        /// Total Gateway security violations by type.
        /// </summary>
        public static readonly Counter SecurityViolations = Prometheus.Metrics
            .CreateCounter("conduit_gateway_security_violations_total", "Total Gateway security violations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "type" }
                });

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
