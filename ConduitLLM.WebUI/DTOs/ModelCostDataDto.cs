using System;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Cost data for a specific model
    /// </summary>
    public class ModelCostDataDto
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int Requests { get; set; }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
    }
}