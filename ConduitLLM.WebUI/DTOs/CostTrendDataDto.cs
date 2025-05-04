using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Cost trend data for charting
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
        /// Number of requests
        /// </summary>
        public int Requests { get; set; }
    }
}