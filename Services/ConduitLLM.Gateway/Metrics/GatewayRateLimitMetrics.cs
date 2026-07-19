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

        public static void RecordAllowed(string scope) => Decisions.WithLabels("allowed", string.IsNullOrEmpty(scope) ? "none" : scope).Inc();
        public static void RecordRejected(string scope) => Decisions.WithLabels("rejected", string.IsNullOrEmpty(scope) ? "none" : scope).Inc();
        public static void RecordError() => Errors.Inc();
    }
}
