namespace ConduitLLM.Configuration.DTOs.Costs
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
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total tokens processed
        /// </summary>
        public int TotalTokens { get; set; }
        
        /// <summary>
        /// Cost per 1K tokens
        /// </summary>
        public decimal CostPerToken { get; set; }
        
        /// <summary>
        /// Average cost per request
        /// </summary>
        public decimal AverageCostPerRequest { get; set; }
    }
}