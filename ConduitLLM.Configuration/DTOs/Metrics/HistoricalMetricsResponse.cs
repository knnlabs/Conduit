namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Response containing historical metrics.
    /// </summary>
    public class HistoricalMetricsResponse
    {
        /// <summary>
        /// Query time range.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Query end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Aggregation interval.
        /// </summary>
        public string Interval { get; set; } = string.Empty;

        /// <summary>
        /// Metrics series data.
        /// </summary>
        public List<MetricsSeries> Series { get; set; } = new();
    }
}