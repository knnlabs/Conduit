using System.Collections.Concurrent;

using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Metrics bucket for time-based aggregation.
    /// </summary>
    internal class MetricsBucket
    {
        public DateTime Timestamp { get; set; }
        public ConcurrentBag<TranscriptionMetric> TranscriptionMetrics { get; } = new();
        public ConcurrentBag<TtsMetric> TtsMetrics { get; } = new();
        public ConcurrentBag<RealtimeMetric> RealtimeMetrics { get; } = new();
        public ConcurrentBag<RoutingMetric> RoutingMetrics { get; } = new();
        public ConcurrentBag<ProviderHealthMetric> ProviderHealthMetrics { get; } = new();

        public ConcurrentDictionary<AudioOperation, int> ActiveOperations { get; } = new();
        public ConcurrentDictionary<string, int> ProviderRequests { get; } = new();
        public ConcurrentDictionary<string, int> RoutingStrategies { get; } = new();

        public long TotalRequests;
        public long SuccessfulRequests;
        public long FailedRequests;
        public long CacheHits;
        public long CdnUploads;
        public long TotalRealtimeSeconds;
        public long TotalRealtimeTurns;

        public void UpdateOperation(AudioOperation operation, bool success, double durationMs)
        {
            Interlocked.Increment(ref TotalRequests);
            if (success)
            {
                Interlocked.Increment(ref SuccessfulRequests);
            }
            else
            {
                Interlocked.Increment(ref FailedRequests);
            }

            ActiveOperations.AddOrUpdate(operation, 1, (_, count) => count + 1);
        }

        public void TrackRoutingDecision(string provider, string strategy)
        {
            ProviderRequests.AddOrUpdate(provider, 1, (_, count) => count + 1);
            RoutingStrategies.AddOrUpdate(strategy, 1, (_, count) => count + 1);
        }

        public void UpdateProviderHealth(string provider, bool healthy, double errorRate)
        {
            // Provider health is tracked in the ProviderHealthMetrics collection
        }
    }

    /// <summary>
    /// Options for audio metrics collection.
    /// </summary>
    public class AudioMetricsOptions
    {
        /// <summary>
        /// Gets or sets the aggregation interval.
        /// </summary>
        public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the retention period for metrics.
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets the transcription latency threshold.
        /// </summary>
        public double TranscriptionLatencyThreshold { get; set; } = 5000; // 5 seconds

        /// <summary>
        /// Gets or sets the realtime latency threshold.
        /// </summary>
        public double RealtimeLatencyThreshold { get; set; } = 200; // 200ms
    }
}