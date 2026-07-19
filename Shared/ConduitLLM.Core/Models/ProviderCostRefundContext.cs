namespace ConduitLLM.Core.Models;

/// <summary>
/// Context supplied to a refund calculation when the original request was billed from a trusted
/// provider-reported cost rather than from ModelCost.
/// </summary>
/// <remarks>
/// Provider-cost billing has no per-unit rate breakdown (the provider returns a single amount), so a
/// refund cannot be recomputed from token rates. Instead it is prorated from the amount actually
/// charged, by the fraction of billable tokens being refunded.
/// </remarks>
public sealed class ProviderCostRefundContext
{
    /// <summary>
    /// The amount (USD) actually charged for the original request — the provider-reported cost times
    /// the configured markup, as persisted on the original request log.
    /// </summary>
    public decimal OriginalChargedCost { get; init; }
}
