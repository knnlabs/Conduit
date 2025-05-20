namespace ConduitLLM.Configuration.DTOs.Costs
{
    /// <summary>
    /// Cost data for a specific virtual key
    /// </summary>
    public class VirtualKeyCostDataDto
    {
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Virtual key name
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Total cost
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Number of requests
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Average cost per request
        /// </summary>
        public decimal AverageCostPerRequest { get; set; }
    }
}