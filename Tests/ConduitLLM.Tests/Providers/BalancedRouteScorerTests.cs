using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;

using FluentAssertions;

using Moq;

namespace ConduitLLM.Tests.Providers;

public sealed class BalancedRouteScorerTests
{
    [Fact]
    public void Score_NormalizesDimensionsAndChoosesBestBalancedCandidate()
    {
        var cheap = Mapping(1, price: 1m, speed: 0.5m, quality: 0.8m);
        var premium = Mapping(2, price: 4m, speed: 1.5m, quality: 1m);
        var policy = new ModelRoutePolicy { CostWeight = 0.8m, SpeedWeight = 0.1m, QualityWeight = 0.1m };

        BalancedRouteScorer.Score([premium, cheap], policy)[0].Mapping.Id.Should().Be(1);
    }

    [Fact]
    public void Score_MissingMetadataIsNeutralAndTiesUsePriorityThenId()
    {
        var first = Mapping(8, null, null, null); first.RoutingPriority = 20;
        var priorityWinner = Mapping(9, null, null, null); priorityWinner.RoutingPriority = 10;

        var result = BalancedRouteScorer.Score([first, priorityWinner], new ModelRoutePolicy());

        result.Should().OnlyContain(route => route.Score == 0.5m);
        result[0].Mapping.Id.Should().Be(9);
    }

    [Fact]
    public void Score_AppliesMappingWeight()
    {
        var normal = Mapping(1, null, null, null);
        var weighted = Mapping(2, null, null, null); weighted.RoutingWeight = 1.5m;

        BalancedRouteScorer.Score([normal, weighted], new ModelRoutePolicy())[0].Mapping.Id.Should().Be(2);
    }

    [Fact]
    public async Task Score_DoesNotClaimHalfOpenProbeForRouteSkippedAfterEarlierSuccess()
    {
        var winner = Mapping(Random.Shared.Next(10_000_000, 20_000_000), null, null, null);
        winner.RoutingWeight = 2m;
        var recovered = Mapping(Random.Shared.Next(20_000_001, 30_000_000), null, null, null);
        var failedAt = DateTime.UtcNow.AddSeconds(-31);
        for (var failure = 0; failure < 5; failure++)
            RouteCircuitRegistry.Failure(recovered.Id, failedAt);

        try
        {
            var winnerProvider = new Mock<ILLMClient>();
            winnerProvider
                .Setup(client => client.CreateChatCompletionAsync(
                    It.IsAny<ChatCompletionRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatCompletionResponse
                {
                    Id = "winner",
                    Choices = [],
                    Created = 1,
                    Model = "model",
                    Object = "chat.completion"
                });
            var recoveredProvider = new Mock<ILLMClient>();
            var policy = new ModelRoutePolicy();
            var scored = BalancedRouteScorer.Score([recovered, winner], policy);
            var request = new ChatCompletionRequest
            {
                Model = "alias",
                Messages = [new Message { Role = "user", Content = "hello" }]
            };
            var routed = new RoutedChatClient(
                scored.Select(route => (route.Mapping,
                    route.Mapping.Id == winner.Id ? winnerProvider.Object : recoveredProvider.Object)).ToArray(),
                request,
                null,
                policy);

            var response = await routed.CreateChatCompletionAsync(request);

            response.Id.Should().Be("winner");
            recoveredProvider.Verify(client => client.CreateChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            BalancedRouteScorer.Score([recovered, winner], policy)
                .Should().Contain(route => route.Mapping.Id == recovered.Id);
            RouteCircuitRegistry.TryBeginAttempt(recovered.Id, out _).Should().BeTrue();
        }
        finally
        {
            RouteCircuitRegistry.Success(winner.Id);
            RouteCircuitRegistry.Success(recovered.Id);
        }
    }

    private static ModelProviderMapping Mapping(int id, decimal? price, decimal? speed, decimal? quality)
    {
        var cost = price.HasValue ? new ModelCost
        {
            InputCostPerMillionTokens = price.Value,
            OutputCostPerMillionTokens = 0m
        } : null;
        return new ModelProviderMapping
        {
            Id = id, ModelAlias = "alias", ProviderModelId = "model", ProviderId = id,
            Provider = new Provider { Id = id, IsEnabled = true },
            ModelProviderTypeAssociationId = id,
            ModelProviderTypeAssociation = new ModelProviderTypeAssociation
            {
                Id = id, IsEnabled = true, SpeedScore = speed, QualityScore = quality, ModelCost = cost
            }
        };
    }
}
