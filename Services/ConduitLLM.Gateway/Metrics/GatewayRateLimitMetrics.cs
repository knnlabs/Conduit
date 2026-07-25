using Prometheus;

namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// Prometheus metrics for Gateway virtual-key rate limiting.
    /// </summary>
    public static class GatewayRateLimitMetrics
    {
        private static readonly Counter Decisions = Prometheus.Metrics.CreateCounter(
            "conduit_gateway_rate_limit_decisions_total",
            "Number of rate-limit decisions by outcome",
            new CounterConfiguration { LabelNames = new[] { "outcome", "scope" } });

        private static readonly Counter Errors = Prometheus.Metrics.CreateCounter(
            "conduit_gateway_rate_limit_errors_total",
            "Number of rate-limit check errors (Redis unavailable, etc.) — these fail open");

        private static readonly Counter Degraded = Prometheus.Metrics.CreateCounter(
            "conduit_gateway_rate_limit_degraded_total",
            "Checks that could not be evaluated, by scope and the failure mode that was applied",
            new CounterConfiguration { LabelNames = new[] { "scope", "mode" } });

        public static void RecordAllowed(string scope) => Decisions.WithLabels("allowed", string.IsNullOrEmpty(scope) ? "none" : scope).Inc();
        public static void RecordRejected(string scope) => Decisions.WithLabels("rejected", string.IsNullOrEmpty(scope) ? "none" : scope).Inc();
        public static void RecordError() => Errors.Inc();

        /// <summary>
        /// A limit could not be evaluated. Alert on this: it means enforcement is not running,
        /// which is otherwise invisible until it shows up in the bill.
        /// </summary>
        public static void RecordDegraded(string scope, string mode) =>
            Degraded.WithLabels(string.IsNullOrEmpty(scope) ? "none" : scope, mode).Inc();
    }
}
