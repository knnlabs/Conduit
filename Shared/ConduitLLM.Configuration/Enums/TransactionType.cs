namespace ConduitLLM.Configuration.Enums
{
    /// <summary>
    /// Represents the type of a virtual key group transaction
    /// </summary>
    public enum TransactionType
    {
        /// <summary>
        /// Credits added to the group balance
        /// </summary>
        Credit = 1,

        /// <summary>
        /// Usage deducted from the group balance
        /// </summary>
        Debit = 2,

        /// <summary>
        /// Refund of previous charges
        /// </summary>
        Refund = 3,

        /// <summary>
        /// Manual adjustment (can be positive or negative)
        /// </summary>
        Adjustment = 4
    }
}