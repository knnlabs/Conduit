using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Services;

using FsCheck;
using FsCheck.Xunit;

namespace ConduitLLM.BillingInvariantTests;

public sealed class AgenticCompositionProperties
{
    [Property(MaxTest = 500)]
    public async Task Provider_call_costs_are_summed_without_repricing_aggregate(NonEmptyArray<PositiveInt> tokenCounts)
    {
        var calls = tokenCounts.Get.Select((tokens, index) => new ProviderCallUsage
        {
            Iteration = index + 1,
            Usage = new Usage { PromptTokens = tokens.Get % 500_000 + 1 }
        }).ToList();
        var aggregate = new Usage { PromptTokens = calls.Sum(call => call.Usage.PromptTokens) };

        static Task<decimal> TieredCallCost(Usage usage)
        {
            var tokens = usage.PromptTokens ?? 0;
            var rate = tokens <= 200_000 ? 1m : 2m;
            return Task.FromResult(tokens * rate / 1_000_000m);
        }

        var actual = await BillingCostComposition.CalculateProviderCostAsync(calls, aggregate, TieredCallCost);
        var expected = calls.Sum(call => TieredCallCost(call.Usage).GetAwaiter().GetResult());

        Assert.Equal(expected, actual);
    }
}
