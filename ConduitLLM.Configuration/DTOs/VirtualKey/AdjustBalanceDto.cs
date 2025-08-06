namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// Data transfer object for balance adjustment requests
    /// </summary>
    public class AdjustBalanceDto
    {
        /// <summary>
        /// Amount to adjust the balance by.
        /// Positive values add credits, negative values debit the account.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional description for the transaction
        /// </summary>
        public string? Description { get; set; }
    }
}