using Prometheus;

namespace ConduitLLM.Core.Metrics;

/// <summary>
/// Operational signals for media-delete budget store failures.
/// </summary>
public static class MediaDeletionBudgetMetrics
{
    public static readonly Counter StoreFailures = Prometheus.Metrics.CreateCounter(
        "conduit_admin_media_cleanup_budget_store_failures_total",
        "Media cleanup budget store failures by backend and configured behavior",
        new CounterConfiguration
        {
            LabelNames = new[] { "backend", "failure_mode", "operation" }
        });
}
