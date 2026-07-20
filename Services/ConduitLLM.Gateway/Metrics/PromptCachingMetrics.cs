using Prometheus;

namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// Prometheus metrics for prompt caching observability.
    /// Tracks cache hit/miss at the request level and estimated cost savings from cached token usage.
    /// Injection metrics are in <see cref="ConduitLLM.Core.Metrics.PromptCachingInjectionMetrics"/>.
    /// </summary>
    public static class PromptCachingMetrics
    {
        /// <summary>
        /// Total requests by cache status (hit = cached tokens returned, miss = no cached tokens, disabled = caching not active).
        /// </summary>
        public static readonly Counter RequestsTotal = Prometheus.Metrics
            .CreateCounter("conduit_prompt_caching_requests_total", "Total requests by prompt caching status",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider", "cache_status" } // cache_status: hit, miss, disabled
                });

        /// <summary>
        /// Estimated cost savings in dollars from cached token usage.
        /// Calculated as: cached_input_tokens * (standard_input_rate - cached_input_rate) per million tokens.
        /// </summary>
        public static readonly Counter SavingsDollarsTotal = Prometheus.Metrics
            .CreateCounter("conduit_prompt_caching_savings_dollars", "Estimated cost savings from prompt caching in dollars",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider" }
                });

        public static readonly Counter EventsTotal = Prometheus.Metrics
            .CreateCounter("conduit_prompt_cache_events_total", "Independent prompt cache events",
                new CounterConfiguration { LabelNames = new[] { "alias", "provider", "mapping", "event" } });

        public static readonly Counter RoutingDecisionsTotal = Prometheus.Metrics
            .CreateCounter("conduit_chat_routing_decisions_total", "Chat routing decisions",
                new CounterConfiguration { LabelNames = new[] { "alias", "provider", "mapping", "reason" } });

        public static readonly Counter RoutingFailoversTotal = Prometheus.Metrics
            .CreateCounter("conduit_chat_routing_failovers_total", "Chat provider route failovers",
                new CounterConfiguration { LabelNames = new[] { "alias", "provider", "mapping" } });

        public static readonly Counter WritePremiumDollarsTotal = Prometheus.Metrics
            .CreateCounter("conduit_prompt_caching_write_premium_dollars", "Additional prompt cache write cost in dollars",
                new CounterConfiguration { LabelNames = new[] { "model", "provider" } });

        // Convenience methods

        /// <summary>
        /// Record a request where cached tokens were returned by the provider.
        /// </summary>
        public static void RecordCacheHit(string model, string provider, string mapping = "unknown")
        { RequestsTotal.WithLabels(model, provider, "hit").Inc(); EventsTotal.WithLabels(model, provider, mapping, "read").Inc(); }

        /// <summary>
        /// Record a request where no cached tokens were returned.
        /// </summary>
        public static void RecordCacheMiss(string model, string provider, string mapping = "unknown")
        { RequestsTotal.WithLabels(model, provider, "miss").Inc(); EventsTotal.WithLabels(model, provider, mapping, "eligible_miss").Inc(); }

        public static void RecordCacheWrite(string model, string provider, string mapping = "unknown")
        { RequestsTotal.WithLabels(model, provider, "write").Inc(); EventsTotal.WithLabels(model, provider, mapping, "write").Inc(); }

        public static void RecordCacheUnknown(string model, string provider, string mapping = "unknown")
        { RequestsTotal.WithLabels(model, provider, "unknown").Inc(); EventsTotal.WithLabels(model, provider, mapping, "unknown").Inc(); }

        public static void RecordCacheUnsupported(string model, string provider, string mapping = "unknown")
        { RequestsTotal.WithLabels(model, provider, "unsupported").Inc(); EventsTotal.WithLabels(model, provider, mapping, "unsupported").Inc(); }

        public static void RecordRouting(string alias, string provider, int mappingId, string reason, int failovers)
        {
            var mapping = mappingId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            RoutingDecisionsTotal.WithLabels(alias, provider, mapping, reason).Inc();
            if (failovers > 0) RoutingFailoversTotal.WithLabels(alias, provider, mapping).Inc(failovers);
        }

        /// <summary>
        /// Record a request where prompt caching was not active (disabled or not configured).
        /// </summary>
        public static void RecordCacheDisabled(string model, string provider)
            => RequestsTotal.WithLabels(model, provider, "disabled").Inc();

        /// <summary>
        /// Record estimated cost savings from cached token usage.
        /// </summary>
        public static void RecordSavings(string model, string provider, double savingsDollars)
        {
            if (savingsDollars > 0)
            {
                SavingsDollarsTotal.WithLabels(model, provider).Inc(savingsDollars);
            }
        }

        public static void RecordWritePremium(string model, string provider, double premiumDollars)
        {
            if (premiumDollars > 0) WritePremiumDollarsTotal.WithLabels(model, provider).Inc(premiumDollars);
        }
    }
}
