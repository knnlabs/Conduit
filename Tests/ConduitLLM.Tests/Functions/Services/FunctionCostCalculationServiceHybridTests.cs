using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Functions.Services;

public class FunctionCostCalculationServiceHybridTests
{
    [Fact]
    public async Task CalculateCostAsync_PerplexityConfig_BillsBaseAndTypedTokens()
    {
        var service = CreateService(CreatePerplexityCost());
        var usage = new FunctionExecutionUsage
        {
            InputTokensConsumed = 1_000,
            OutputTokensConsumed = 500
        };

        var cost = await service.CalculateCostAsync(42, usage);

        Assert.Equal(0.006995m, cost);
    }

    [Fact]
    public async Task EstimateCostAsync_PerplexityConfig_ReservesBaseAndMaximumOutputCost()
    {
        var service = CreateService(CreatePerplexityCost());

        var cost = await service.EstimateCostAsync(42, new Dictionary<string, object>
        {
            ["messages"] = new object(),
            ["maxTokens"] = 4_096
        });

        Assert.Equal(0.01044768m, cost);
    }

    [Fact]
    public async Task CalculateCostAsync_MalformedTavilyConfig_FailsClosed()
    {
        var malformedCost = new FunctionCost
        {
            CostName = "Malformed Tavily",
            ProviderType = FunctionProviderType.Tavily,
            PricingModel = FunctionPricingModel.Hybrid,
            PricingConfiguration = "{\"baseRequestCost\":0.005}"
        };
        var service = CreateService(malformedCost);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CalculateCostAsync(42, new FunctionExecutionUsage()));

        Assert.Contains("Invalid Tavily hybrid pricing configuration", exception.Message);
    }

    [Fact]
    public async Task EstimateCostAsync_MalformedPerplexityConfig_FailsClosed()
    {
        var malformedCost = CreatePerplexityCost();
        malformedCost.PricingConfiguration = "{\"baseRequestCost\":0.005}";
        var service = CreateService(malformedCost);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EstimateCostAsync(42, new Dictionary<string, object> { ["messages"] = new object() }));

        Assert.Contains("Invalid Perplexity hybrid pricing configuration", exception.Message);
    }

    private static FunctionCostCalculationService CreateService(FunctionCost cost)
    {
        var costService = new Mock<IFunctionCostService>();
        costService
            .Setup(service => service.GetCostForConfigurationAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cost);

        return new FunctionCostCalculationService(
            costService.Object,
            Mock.Of<ILogger<FunctionCostCalculationService>>());
    }

    private static FunctionCost CreatePerplexityCost() => new()
    {
        CostName = "Perplexity Sonar",
        ProviderType = FunctionProviderType.Perplexity,
        PricingModel = FunctionPricingModel.Hybrid,
        PricingConfiguration = """
            {
              "baseRequestCost": 0.005,
              "inputTokenCostPerMillion": 1.33,
              "outputTokenCostPerMillion": 1.33
            }
            """
    };
}
