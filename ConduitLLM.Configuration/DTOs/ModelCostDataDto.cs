using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Cost data for a specific model
    /// </summary>
    public class ModelCostDataDto
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Number of input tokens
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Number of output tokens
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Model name (alias for backwards compatibility)
        /// </summary>
        public string Model
        {
            get => ModelName;
            set => ModelName = value;
        }
        
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