using System;

namespace ConduitLLM.Configuration.DTOs.Costs
{
    /// <summary>
    /// Cost trend data point for charting
    /// </summary>
    public class CostTrendDataDto
    {
        /// <summary>
        /// Date of the cost data point
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Cost amount
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Number of requests in this period
        /// </summary>
        public int RequestCount { get; set; }
    }
}
