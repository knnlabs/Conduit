using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.BillingInvariantTests;

public sealed class RefundProperties
{
    [Property(MaxTest = 500)]
    public async Task Refund_prorated_from_original_charge_never_exceeds_charge(
        PositiveInt originalPrompt,
        NonNegativeInt requestedPrompt,
        PositiveInt cents)
    {
        var calculator = new CostCalculationService(
            Mock.Of<IModelCostService>(), Mock.Of<ILogger<CostCalculationService>>());
        var originalTokens = originalPrompt.Get % 1_000_000 + 1;
        var original = new Usage { PromptTokens = originalTokens, TotalTokens = originalTokens };
        var refundTokens = requestedPrompt.Get % (originalTokens + 1);
        var refund = new Usage { PromptTokens = refundTokens, TotalTokens = refundTokens };
        var charge = (cents.Get % 10_000_000 + 1) / 100m;

        var result = await calculator.CalculateRefundAsync(
            "model", original, refund, "property",
            providerCostContext: new ProviderCostRefundContext { OriginalChargedCost = charge });

        Assert.InRange(result.RefundAmount, 0m, charge);
    }
}
