using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Historical metrics series.
    /// </summary>
    public class MetricsSeries
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Series label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Data points in the series.
        /// </summary>
        public List<MetricsDataPoint> DataPoints { get; set; } = new();
    }
}