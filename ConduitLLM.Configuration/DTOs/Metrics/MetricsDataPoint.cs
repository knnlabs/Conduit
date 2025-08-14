using System;

namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Historical metrics data point.
    /// </summary>
    public class MetricsDataPoint
    {
        /// <summary>
        /// Timestamp of the data point.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Metric value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Optional label for categorization.
        /// </summary>
        public string? Label { get; set; }
    }
}