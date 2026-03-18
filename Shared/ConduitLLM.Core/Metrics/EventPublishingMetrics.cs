using Prometheus;

namespace ConduitLLM.Core.Metrics
{
    /// <summary>
    /// Prometheus metrics for MassTransit event publishing operations.
    /// Tracks publish success/failure rates and latency.
    /// </summary>
    public static class EventPublishingMetrics
    {
        /// <summary>
        /// Total event publish operations by event type and status.
        /// </summary>
        public static readonly Counter EventsPublished = Prometheus.Metrics
            .CreateCounter("conduit_admin_events_published_total", "Total event publish operations",
                new CounterConfiguration
                {
                    LabelNames = new[] { "event_type", "status" } // status: success, failure, skipped
                });

        /// <summary>
        /// Event publish duration in seconds.
        /// </summary>
        public static readonly Histogram EventPublishDuration = Prometheus.Metrics
            .CreateHistogram("conduit_admin_event_publish_duration_seconds", "Event publish duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "event_type" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 12) // 1ms to ~4s
                });

        // Convenience methods

        public static void RecordSuccess(string eventType, double? durationSeconds = null)
        {
            EventsPublished.WithLabels(eventType, "success").Inc();
            if (durationSeconds.HasValue)
                EventPublishDuration.WithLabels(eventType).Observe(durationSeconds.Value);
        }

        public static void RecordFailure(string eventType)
            => EventsPublished.WithLabels(eventType, "failure").Inc();

        public static void RecordSkipped(string eventType)
            => EventsPublished.WithLabels(eventType, "skipped").Inc();
    }
}
