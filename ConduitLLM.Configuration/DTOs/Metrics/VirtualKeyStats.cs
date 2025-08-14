namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Virtual key statistics.
    /// </summary>
    public class VirtualKeyStats
    {
        /// <summary>
        /// Virtual key ID.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Key name.
        /// </summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>
        /// Requests per minute.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Total spend.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Budget utilization percentage.
        /// </summary>
        public double BudgetUtilization { get; set; }

        /// <summary>
        /// Is over budget.
        /// </summary>
        public bool IsOverBudget { get; set; }
    }
}