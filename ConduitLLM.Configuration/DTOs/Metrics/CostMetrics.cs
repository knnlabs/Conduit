namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Cost metrics.
    /// </summary>
    public class CostMetrics
    {
        /// <summary>
        /// Total cost rate in dollars per minute.
        /// </summary>
        public decimal TotalCostPerMinute { get; set; }

        /// <summary>
        /// Cost breakdown by provider.
        /// </summary>
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();

        /// <summary>
        /// Average cost per request.
        /// </summary>
        public decimal AverageCostPerRequest { get; set; }
    }
}