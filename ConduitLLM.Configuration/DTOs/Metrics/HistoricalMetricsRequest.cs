namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Request for historical metrics.
    /// </summary>
    public class HistoricalMetricsRequest
    {
        /// <summary>
        /// Start time for the query.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time for the query.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Metric names to retrieve.
        /// </summary>
        public List<string> MetricNames { get; set; } = new();

        /// <summary>
        /// Time interval for aggregation (e.g., "1m", "5m", "1h").
        /// </summary>
        public string Interval { get; set; } = "1m";

        /// <summary>
        /// Optional filters by label.
        /// </summary>
        public Dictionary<string, string>? Filters { get; set; }
    }
}