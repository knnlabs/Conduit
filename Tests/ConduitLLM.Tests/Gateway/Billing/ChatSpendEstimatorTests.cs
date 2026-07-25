using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Billing;
using ConduitLLM.Gateway.Options;

using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Http.Billing;

public class ChatSpendEstimatorTests
{
    [Fact]
    public async Task EstimateUsesPromptTokensAndBoundedModelOutputMaximum()
    {
        var mappingService = new Mock<IModelProviderMappingService>();
        mappingService.Setup(x => x.GetMappingByModelAliasAsync("model"))
            .ReturnsAsync(new ModelProviderMapping
            {
                ModelAlias = "model",
                ProviderModelId = "provider-model",
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation
                {
                    ModelCostId = 9,
                    MaxOutputTokens = 8192,
                    Model = new Model { MaxOutputTokens = 16384 }
                }
            });
        var tokenCounter = new Mock<ITokenCounter>();
        tokenCounter.Setup(x => x.EstimateTokenCountAsync("model", It.IsAny<List<Message>>()))
            .ReturnsAsync(new TokenCount(100, TokenCountFidelity.Exact));
        var costService = new Mock<ICostCalculationService>();
        costService.Setup(x => x.CalculateCostByIdAsync(
                9,
                It.Is<Usage>(usage => usage.PromptTokens == 100 && usage.CompletionTokens == 4096),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.25m);
        var estimator = new ChatSpendEstimator(
            mappingService.Object,
            tokenCounter.Object,
            costService.Object,
            Options.Create(new BillingAdmissionOptions
            {
                MaximumOutputTokensCap = 4096,
                DefaultMaximumOutputTokens = 2048
            }));

        var result = await estimator.EstimateMaximumCostAsync(new ChatCompletionRequest
        {
            Model = "model",
            Messages = [new Message { Role = "user", Content = "hello" }]
        });

        Assert.True(result.Succeeded);
        Assert.Equal(0.25m, result.Amount);
        Assert.Equal(4096, result.MaximumOutputTokens);
        Assert.Equal(9, result.ModelCostId);
    }

    [Fact]
    public async Task EstimatePrefersResponsesCompatibleMaxCompletionTokens()
    {
        var mappingService = new Mock<IModelProviderMappingService>();
        mappingService.Setup(x => x.GetMappingByModelAliasAsync("model"))
            .ReturnsAsync(new ModelProviderMapping
            {
                ModelAlias = "model",
                ProviderModelId = "provider-model",
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation { ModelCostId = 9 }
            });
        var tokenCounter = new Mock<ITokenCounter>();
        tokenCounter.Setup(x => x.EstimateTokenCountAsync("model", It.IsAny<List<Message>>()))
            .ReturnsAsync(new TokenCount(10, TokenCountFidelity.Exact));
        var costService = new Mock<ICostCalculationService>();
        costService.Setup(x => x.CalculateCostByIdAsync(
                9,
                It.Is<Usage>(usage => usage.CompletionTokens == 64),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.01m);
        var estimator = new ChatSpendEstimator(
            mappingService.Object,
            tokenCounter.Object,
            costService.Object,
            Options.Create(new BillingAdmissionOptions()));

        var result = await estimator.EstimateMaximumCostAsync(new ChatCompletionRequest
        {
            Model = "model",
            Messages = [new Message { Role = "user", Content = "hello" }],
            MaxTokens = 128,
            MaxCompletionTokens = 64
        });

        Assert.True(result.Succeeded);
        Assert.Equal(64, result.MaximumOutputTokens);
    }

    [Theory]
    [InlineData(TokenCountFidelity.Exact, 1000, 1000)]
    [InlineData(TokenCountFidelity.ApproximateVocabulary, 1000, 1150)]  // +15% default buffer
    [InlineData(TokenCountFidelity.CharacterHeuristic, 1000, 1500)]     // +50% default buffer
    public async Task EstimateBuffersPromptTokensByCountFidelity(
        TokenCountFidelity fidelity, int rawTokens, int expectedReservedPromptTokens)
    {
        var mappingService = new Mock<IModelProviderMappingService>();
        mappingService.Setup(x => x.GetMappingByModelAliasAsync("model"))
            .ReturnsAsync(new ModelProviderMapping
            {
                ModelAlias = "model",
                ProviderModelId = "provider-model",
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation { ModelCostId = 9 }
            });
        var tokenCounter = new Mock<ITokenCounter>();
        tokenCounter.Setup(x => x.EstimateTokenCountAsync("model", It.IsAny<List<Message>>()))
            .ReturnsAsync(new TokenCount(rawTokens, fidelity));
        var costService = new Mock<ICostCalculationService>();
        costService.Setup(x => x.CalculateCostByIdAsync(
                9,
                It.Is<Usage>(usage => usage.PromptTokens == expectedReservedPromptTokens),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.10m);
        var estimator = new ChatSpendEstimator(
            mappingService.Object,
            tokenCounter.Object,
            costService.Object,
            Options.Create(new BillingAdmissionOptions()));

        var result = await estimator.EstimateMaximumCostAsync(new ChatCompletionRequest
        {
            Model = "model",
            Messages = [new Message { Role = "user", Content = "hello" }],
            MaxCompletionTokens = 64
        });

        Assert.True(result.Succeeded);
        Assert.Equal(expectedReservedPromptTokens, result.PromptTokens);
        // The cost mock only matches the buffered prompt count, so a wrong buffer fails here too.
        Assert.Equal(0.10m, result.Amount);
    }
}
