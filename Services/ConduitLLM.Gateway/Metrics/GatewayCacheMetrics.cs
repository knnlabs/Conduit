using ConduitLLM.Core.Metrics;
using Prometheus;

namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// Prometheus metrics for Gateway cache operations (Redis and in-memory).
    /// Delegates to shared <see cref="CacheMetrics"/> with "gateway" prefix.
    /// </summary>
    public static class GatewayCacheMetrics
    {
        private static readonly CacheMetrics Instance = new("gateway");

        public static Counter CacheLookups => Instance.CacheLookups;
        public static Histogram CacheLatency => Instance.CacheLatency;
        public static Counter CacheInvalidations => Instance.CacheInvalidations;
        public static Counter CacheErrors => Instance.CacheErrors;

        public static void RecordHit(string cacheName) => Instance.RecordHit(cacheName);
        public static void RecordMiss(string cacheName) => Instance.RecordMiss(cacheName);
        public static void RecordLatency(string cacheName, string operation, double durationSeconds) => Instance.RecordLatency(cacheName, operation, durationSeconds);
        public static void RecordInvalidation(string cacheName, string reason = "explicit") => Instance.RecordInvalidation(cacheName, reason);
        public static void RecordError(string cacheName, string operation) => Instance.RecordError(cacheName, operation);
    }
}
