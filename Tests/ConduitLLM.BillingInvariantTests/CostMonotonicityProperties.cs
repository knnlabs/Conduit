using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.BillingInvariantTests;

public sealed class CostMonotonicityProperties
{
    [Property(MaxTest = 500)]
    public async Task Standard_cost_is_monotonic_in_normalized_token_quantities(
        NonNegativeInt regularInput,
        NonNegativeInt cachedInput,
        NonNegativeInt regularOutput,
        NonNegativeInt reasoning,
        PositiveInt increment,
        NonNegativeInt dimension)
    {
        var modelCost = new ModelCost
        {
            Id = 1,
            CostName = "property-standard",
            InputCostPerMillionTokens = 2m,
            OutputCostPerMillionTokens = 4m,
            CachedInputCostPerMillionTokens = 0.5m,
            ReasoningCostPerMillionTokens = 6m,
            IsActive = true
        };
        var costs = new Mock<IModelCostService>();
        costs.Setup(service => service.GetCostForModelAsync("model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(modelCost);
        var calculator = new CostCalculationService(
            costs.Object,
            Mock.Of<ILogger<CostCalculationService>>());

        var normalizedRegularInput = regularInput.Get % 1_000_000;
        var normalizedCachedInput = cachedInput.Get % 1_000_000;
        var normalizedRegularOutput = regularOutput.Get % 1_000_000;
        var normalizedReasoning = reasoning.Get % 1_000_000;
        var normalizedIncrement = increment.Get % 1_000_000 + 1;
        var baseline = UsageFor(normalizedRegularInput, normalizedCachedInput, normalizedRegularOutput, normalizedReasoning);
        var quantities = new[]
        {
            normalizedRegularInput, normalizedCachedInput, normalizedRegularOutput, normalizedReasoning
        };
        quantities[dimension.Get % quantities.Length] += normalizedIncrement;
        var increased = UsageFor(quantities[0], quantities[1], quantities[2], quantities[3]);

        var baselineCost = await calculator.CalculateCostAsync("model", baseline);
        var increasedCost = await calculator.CalculateCostAsync("model", increased);

        Assert.True(increasedCost >= baselineCost,
            $"Increasing normalized dimension {dimension.Get % quantities.Length} reduced cost from {baselineCost} to {increasedCost}.");
    }

    private static Usage UsageFor(int regularInput, int cachedInput, int regularOutput, int reasoning) => new()
    {
        PromptTokens = checked(regularInput + cachedInput),
        CachedInputTokens = cachedInput,
        CachedInputTokensIncludedInPrompt = true,
        CompletionTokens = checked(regularOutput + reasoning),
        ReasoningTokens = reasoning,
        TotalTokens = checked(regularInput + cachedInput + regularOutput + reasoning)
    };
}
