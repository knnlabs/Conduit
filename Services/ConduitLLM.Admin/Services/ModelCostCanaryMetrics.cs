using Prometheus;

namespace ConduitLLM.Admin.Services;

internal static class ModelCostCanaryMetrics
{
    internal static readonly Gauge Status = Prometheus.Metrics.CreateGauge(
        "conduit_billing_cost_canary_status",
        "Whether the latest synthetic cost check succeeded (1) or failed (0).",
        new GaugeConfiguration { LabelNames = ["model", "model_cost_id"] });

    internal static readonly Gauge FailedModels = Prometheus.Metrics.CreateGauge(
        "conduit_billing_cost_canary_failed_models",
        "Number of active model mappings that failed the latest synthetic cost check.");

    internal static readonly Gauge ActiveModels = Prometheus.Metrics.CreateGauge(
        "conduit_billing_cost_canary_active_models",
        "Number of active model mappings checked by the latest synthetic cost canary run.");

    internal static readonly Gauge LastRunTimestamp = Prometheus.Metrics.CreateGauge(
        "conduit_billing_cost_canary_last_run_timestamp_seconds",
        "Unix timestamp of the last completed synthetic cost canary run.");

    internal static readonly Gauge LastSuccessTimestamp = Prometheus.Metrics.CreateGauge(
        "conduit_billing_cost_canary_last_success_timestamp_seconds",
        "Unix timestamp of the last successful synthetic cost check.",
        new GaugeConfiguration { LabelNames = ["model", "model_cost_id"] });

    internal static readonly Counter Runs = Prometheus.Metrics.CreateCounter(
        "conduit_billing_cost_canary_checks_total",
        "Total synthetic model-cost checks.",
        new CounterConfiguration { LabelNames = ["status", "reason"] });
}
