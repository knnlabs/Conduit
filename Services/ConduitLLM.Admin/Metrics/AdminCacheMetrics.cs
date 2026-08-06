using ConduitLLM.Core.Metrics;
using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for Admin API cache operations.
    /// Delegates to shared <see cref="CacheMetrics"/> with "admin" prefix.
    /// </summary>
    public static class AdminCacheMetrics
    {
        private static readonly CacheMetrics Instance = new("admin");

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
