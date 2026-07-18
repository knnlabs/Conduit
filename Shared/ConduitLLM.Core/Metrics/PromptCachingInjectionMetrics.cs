using Prometheus;

namespace ConduitLLM.Core.Metrics
{
    /// <summary>
    /// Prometheus metrics for prompt cache injection operations in the PromptCachingLLMClient decorator.
    /// </summary>
    public static class PromptCachingInjectionMetrics
    {
        /// <summary>
        /// Total cache_control injection attempts by model and result (success/error).
        /// </summary>
        public static readonly Counter InjectionsTotal = Prometheus.Metrics
            .CreateCounter("conduit_prompt_caching_injections_total", "Total prompt cache injection attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "result" } // result: success, error
                });

        public static void RecordSuccess(string model)
            => InjectionsTotal.WithLabels(model, "success").Inc();

        public static void RecordError(string model)
            => InjectionsTotal.WithLabels(model, "error").Inc();
    }
}
