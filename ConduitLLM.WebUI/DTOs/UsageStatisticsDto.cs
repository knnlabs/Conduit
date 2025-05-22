using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Data transfer object for usage statistics of a virtual key
    /// </summary>
    public class UsageStatisticsDto
    {
        /// <summary>
        /// The virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Start date of the usage statistics period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the usage statistics period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Total number of requests made
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// Total cost of all requests
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total number of input tokens
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Total number of output tokens
        /// </summary>
        public int TotalOutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output)
        /// </summary>
        public int TotalTokens => TotalInputTokens + TotalOutputTokens;
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Requests by model as a dictionary (model name -> count)
        /// </summary>
        public Dictionary<string, int> RequestsByModelDict { get; set; } = new();
        
        /// <summary>
        /// Cost by model as a dictionary (model name -> cost)
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        
        /// <summary>
        /// List of request statistics by model
        /// </summary>
        public List<RequestsByModelDto> RequestsByModel { get; set; } = new();
    }
    
    // RequestsByModelDto is defined in a separate file
}