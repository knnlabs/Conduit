using Prometheus;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Centralized Prometheus metrics for usage tracking and billing audit.
    /// </summary>
    public static class UsageMetrics
    {
        // Metrics for usage tracking
        public static readonly Counter UsageTrackingRequests = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_requests_total", "Total usage tracking requests",
                new CounterConfiguration
                {
                    LabelNames = new[] { "endpoint_type", "status" }
                });

        public static readonly Counter UsageTrackingTokens = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_tokens_total", "Total tokens tracked",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider_type", "token_type" }
                });

        public static readonly Counter UsageTrackingCosts = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_cost_dollars", "Total cost tracked in dollars",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider_type", "endpoint_type" }
                });

        public static readonly Counter UsageTrackingFailures = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_failures_total", "Usage tracking failures",
                new CounterConfiguration
                {
                    LabelNames = new[] { "reason", "endpoint_type" }
                });

        public static readonly Histogram UsageExtractionTime = Prometheus.Metrics
            .CreateHistogram("conduit_usage_extraction_time_seconds", "Time to extract usage from response",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "endpoint_type" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 10) // 1ms to ~1s
                });

        // Billing audit metrics
        public static readonly Counter BillingAuditEvents = Prometheus.Metrics
            .CreateCounter("conduit_billing_audit_events_total", "Total billing audit events",
                new CounterConfiguration
                {
                    LabelNames = new[] { "event_type", "provider_type" }
                });

        public static readonly Counter BillingRevenue = Prometheus.Metrics
            .CreateCounter("conduit_billing_revenue_dollars_total", "Total revenue from successful billing",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider_type" }
                });

        public static readonly Counter BillingRevenueLoss = Prometheus.Metrics
            .CreateCounter("conduit_billing_revenue_loss_dollars_total", "Potential revenue loss from billing failures",
                new CounterConfiguration
                {
                    LabelNames = new[] { "event_type", "reason" }
                });

        public static readonly Histogram BillingCostDistribution = Prometheus.Metrics
            .CreateHistogram("conduit_billing_cost_dollars", "Distribution of billing costs",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "model", "provider_type" },
                    Buckets = new[] { 0.0001, 0.001, 0.01, 0.1, 1, 10, 100 } // $0.0001 to $100
                });

        public static readonly Counter ZeroCostEvents = Prometheus.Metrics
            .CreateCounter("conduit_billing_zero_cost_total", "Total zero cost events",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "reason" }
                });
    }
}