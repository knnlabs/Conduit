using System.Collections.Generic;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Data transfer object for usage statistics of a virtual key
    /// </summary>
    public class UsageStatisticsDto
    {
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
        /// Usage statistics grouped by model
        /// </summary>
        public Dictionary<string, ModelUsage> ModelUsage { get; set; } = new();
    }
    
    /// <summary>
    /// Usage statistics for a specific model
    /// </summary>
    public class ModelUsage
    {
        /// <summary>
        /// Number of requests made with this model
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost of requests made with this model
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Total input tokens for this model
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens for this model
        /// </summary>
        public int OutputTokens { get; set; }
    }
}
