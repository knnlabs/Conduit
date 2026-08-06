using Prometheus;

namespace ConduitLLM.Gateway.Metrics;

/// <summary>
/// Shared observability for background and snapshot metrics collectors.
/// </summary>
internal static class MetricsCollectionInstrumentation
{
    private static readonly Counter CollectionFailures = Prometheus.Metrics.CreateCounter(
        "conduit_metrics_collection_failures_total",
        "Number of failed metrics collection operations.",
        new CounterConfiguration
        {
            LabelNames = ["collector"]
        });

    private static readonly Histogram CollectionDuration = Prometheus.Metrics.CreateHistogram(
        "conduit_metrics_collection_duration_seconds",
        "Duration of metrics collection operations in seconds.",
        new HistogramConfiguration
        {
            LabelNames = ["collector"],
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

    internal static IDisposable Measure(string collector) =>
        CollectionDuration.WithLabels(collector).NewTimer();

    internal static void RecordFailure(string collector) =>
        CollectionFailures.WithLabels(collector).Inc();
}
