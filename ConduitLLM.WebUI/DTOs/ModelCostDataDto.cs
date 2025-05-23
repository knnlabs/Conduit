namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Data transfer object for model cost data used in the WebUI dashboard.
    /// </summary>
    public class ModelCostDataDto
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;
        
        /// <summary>
        /// Cost for this model in the time period
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Number of requests with this model
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Number of input tokens with this model
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Number of output tokens with this model
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output) for this model
        /// </summary>
        public int TotalTokens => InputTokens + OutputTokens;
        
        /// <summary>
        /// Cost percentage of the total cost
        /// </summary>
        public double Percentage { get; set; }
    }
}