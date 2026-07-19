namespace ConduitLLM.Core.Models;

/// <summary>
/// Billing policy that governs whether a provider's self-reported cost is authoritative for a request.
/// </summary>
/// <remarks>
/// Stamped onto <see cref="Usage.ProviderCostPolicy"/> by the Gateway from the resolved provider
/// configuration before cost calculation. Kept in <c>ConduitLLM.Core</c> so the cost calculator
/// (which has no HTTP or Configuration dependency) can consume it via the <see cref="Usage"/> object.
/// </remarks>
public sealed class ProviderCostBillingPolicy
{
    /// <summary>
    /// When true, the provider-reported cost (<see cref="Usage.ProviderReportedCostUsd"/>) is
    /// authoritative for billing; ModelCost calculation is only a fallback when no cost is reported.
    /// </summary>
    public bool TrustProviderReportedCost { get; init; }

    /// <summary>
    /// Multiplier applied to the provider-reported cost when billing (1.0 = pass-through).
    /// Only consulted when <see cref="TrustProviderReportedCost"/> is true.
    /// </summary>
    public decimal MarkupMultiplier { get; init; } = 1.0m;
}
