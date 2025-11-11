namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Result of checking and potentially resetting a virtual key's budget period
    /// </summary>
    public class BudgetCheckResult
    {
        /// <summary>
        /// Whether the budget was reset
        /// </summary>
        public bool WasReset { get; set; }

        /// <summary>
        /// The new budget start date if reset was performed
        /// </summary>
        public DateTime? NewBudgetStartDate { get; set; }
    }
}
