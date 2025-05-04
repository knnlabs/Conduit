using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Detailed cost data for export
    /// </summary>
    public class DetailedCostDataDto
    {
        /// <summary>
        /// Date of the cost data
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Model name
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key name
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int Requests { get; set; }
        
        /// <summary>
        /// Total input tokens
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
    }
}