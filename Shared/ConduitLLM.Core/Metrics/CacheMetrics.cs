using Prometheus;

namespace ConduitLLM.Core.Metrics;

/// <summary>
/// Shared Prometheus metrics for cache operations.
/// Instantiate with a service prefix (e.g., "gateway", "admin") to create
/// service-scoped counters and histograms.
/// </summary>
public class CacheMetrics
{
    /// <summary>
    /// Total cache lookups by cache name and result (hit/miss).
    /// </summary>
    public Counter CacheLookups { get; }

    /// <summary>
    /// Cache operation latency by cache name and operation type.
    /// </summary>
    public Histogram CacheLatency { get; }

    /// <summary>
    /// Total cache invalidations by cache name and reason.
    /// </summary>
    public Counter CacheInvalidations { get; }

    /// <summary>
    /// Total cache errors by cache name and operation.
    /// </summary>
    public Counter CacheErrors { get; }

    /// <summary>
    /// Creates a new <see cref="CacheMetrics"/> instance with metrics prefixed by the given service name.
    /// </summary>
    /// <param name="servicePrefix">Service prefix for metric names (e.g., "gateway", "admin").</param>
    public CacheMetrics(string servicePrefix)
    {
        CacheLookups = Prometheus.Metrics
            .CreateCounter($"conduit_{servicePrefix}_cache_lookups_total", "Total cache lookups",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache", "result" }
                });

        CacheLatency = Prometheus.Metrics
            .CreateHistogram($"conduit_{servicePrefix}_cache_latency_seconds", "Cache operation latency",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "cache", "operation" },
                    Buckets = Histogram.ExponentialBuckets(0.0001, 2, 14) // 0.1ms to ~1.6s
                });

        CacheInvalidations = Prometheus.Metrics
            .CreateCounter($"conduit_{servicePrefix}_cache_invalidations_total", "Total cache invalidations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache", "reason" }
                });

        CacheErrors = Prometheus.Metrics
            .CreateCounter($"conduit_{servicePrefix}_cache_errors_total", "Total cache operation errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache", "operation" }
                });
    }

    /// <summary>Records a cache hit for the specified cache.</summary>
    public void RecordHit(string cacheName)
        => CacheLookups.WithLabels(cacheName, "hit").Inc();

    /// <summary>Records a cache miss for the specified cache.</summary>
    public void RecordMiss(string cacheName)
        => CacheLookups.WithLabels(cacheName, "miss").Inc();

    /// <summary>Records the latency of a cache operation.</summary>
    public void RecordLatency(string cacheName, string operation, double durationSeconds)
        => CacheLatency.WithLabels(cacheName, operation).Observe(durationSeconds);

    /// <summary>Records a cache invalidation event.</summary>
    public void RecordInvalidation(string cacheName, string reason = "explicit")
        => CacheInvalidations.WithLabels(cacheName, reason).Inc();

    /// <summary>Records a cache operation error.</summary>
    public void RecordError(string cacheName, string operation)
        => CacheErrors.WithLabels(cacheName, operation).Inc();
}
