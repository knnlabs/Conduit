using System;
using Conf = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Wrapper for ConduitLLM.Configuration.DTOs.DetailedCostDataDto that adds backward compatibility properties.
    /// This class should be used in the WebUI project to ensure compatibility with older code.
    /// </summary>
    public class DetailedCostDataDto
    {
        /// <summary>
        /// Name identifier for the cost data
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Cost amount
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Percentage of total cost
        /// </summary>
        public decimal Percentage { get; set; }
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int RequestCount { get; set; }
    }
}