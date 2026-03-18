using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for Admin API cache operations.
    /// Tracks hit/miss rates, latency, and invalidation patterns.
    /// </summary>
    public static class AdminCacheMetrics
    {
        /// <summary>
        /// Total cache lookups by cache name and result (hit/miss).
        /// </summary>
        public static readonly Counter CacheLookups = Prometheus.Metrics
            .CreateCounter("conduit_admin_cache_lookups_total", "Total cache lookups",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache", "result" } // result: hit, miss
                });

        /// <summary>
        /// Cache operation latency by cache name and operation type.
        /// </summary>
        public static readonly Histogram CacheLatency = Prometheus.Metrics
            .CreateHistogram("conduit_admin_cache_latency_seconds", "Cache operation latency",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "cache", "operation" }, // operation: get, set, invalidate
                    Buckets = Histogram.ExponentialBuckets(0.0001, 2, 14) // 0.1ms to ~1.6s
                });

        /// <summary>
        /// Total cache invalidations by cache name and reason.
        /// </summary>
        public static readonly Counter CacheInvalidations = Prometheus.Metrics
            .CreateCounter("conduit_admin_cache_invalidations_total", "Total cache invalidations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache", "reason" } // reason: explicit, expired, event
                });

        /// <summary>
        /// Total cache errors by cache name and operation.
        /// </summary>
        public static readonly Counter CacheErrors = Prometheus.Metrics
            .CreateCounter("conduit_admin_cache_errors_total", "Total cache operation errors",
                new CounterConfiguration
                {
                    LabelNames = new[] { "cache", "operation" }
                });

        /// <summary>Records a cache hit for the specified cache.</summary>
        /// <param name="cacheName">The name of the cache that was hit.</param>
        public static void RecordHit(string cacheName)
            => CacheLookups.WithLabels(cacheName, "hit").Inc();

        /// <summary>Records a cache miss for the specified cache.</summary>
        /// <param name="cacheName">The name of the cache that was missed.</param>
        public static void RecordMiss(string cacheName)
            => CacheLookups.WithLabels(cacheName, "miss").Inc();

        /// <summary>Records the latency of a cache operation.</summary>
        /// <param name="cacheName">The name of the cache.</param>
        /// <param name="operation">The operation type (e.g., "get", "set", "invalidate").</param>
        /// <param name="durationSeconds">The operation duration in seconds.</param>
        public static void RecordLatency(string cacheName, string operation, double durationSeconds)
            => CacheLatency.WithLabels(cacheName, operation).Observe(durationSeconds);

        /// <summary>Records a cache invalidation event.</summary>
        /// <param name="cacheName">The name of the cache that was invalidated.</param>
        /// <param name="reason">The invalidation reason (e.g., "explicit", "expired", "event").</param>
        public static void RecordInvalidation(string cacheName, string reason = "explicit")
            => CacheInvalidations.WithLabels(cacheName, reason).Inc();

        /// <summary>Records a cache operation error.</summary>
        /// <param name="cacheName">The name of the cache where the error occurred.</param>
        /// <param name="operation">The operation that failed (e.g., "get", "set").</param>
        public static void RecordError(string cacheName, string operation)
            => CacheErrors.WithLabels(cacheName, operation).Inc();
    }
}
