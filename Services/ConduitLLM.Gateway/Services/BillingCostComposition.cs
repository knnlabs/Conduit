using ConduitLLM.Core.Models;

namespace ConduitLLM.Gateway.Services;

/// <summary>
/// Composes provider-call charges without collapsing call boundaries required by non-linear pricing.
/// </summary>
public static class BillingCostComposition
{
    public static async Task<decimal> CalculateProviderCostAsync(
        IReadOnlyCollection<ProviderCallUsage>? providerCalls,
        Usage aggregateUsage,
        Func<Usage, Task<decimal>> calculateCallCost)
    {
        ArgumentNullException.ThrowIfNull(aggregateUsage);
        ArgumentNullException.ThrowIfNull(calculateCallCost);

        if (providerCalls is not { Count: > 0 })
            return await calculateCallCost(aggregateUsage);

        decimal total = 0m;
        foreach (var call in providerCalls.OrderBy(call => call.Iteration))
            total = checked(total + await calculateCallCost(call.Usage));

        return total;
    }
}
