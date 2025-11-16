namespace ConduitLLM.Configuration.DTOs.Costs
{
    /// <summary>
    /// Detailed cost data for breakdown by category
    /// </summary>
    public class DetailedCostDataDto
    {
        /// <summary>
        /// Name (model, provider, key name, etc.)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Total cost
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
