namespace ConduitLLM.Core.Models;

/// <summary>
/// Historical billing context for refund calculations.
/// </summary>
/// <remarks>
/// Despite the legacy provider-cost-specific name, this context is used for every persisted refund.
/// Refunds are based on the recorded debit rather than mutable current pricing.
/// </remarks>
public sealed class ProviderCostRefundContext
{
    /// <summary>
    /// The amount (USD) recorded on the original debit transaction.
    /// </summary>
    public decimal OriginalChargedCost { get; init; }
}
