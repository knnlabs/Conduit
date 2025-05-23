using System;

namespace ConduitLLM.Configuration.DTOs
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
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Number of requests (alias for backwards compatibility)
        /// </summary>
        public int Requests
        {
            get => RequestCount;
            set => RequestCount = value;
        }
    }
}