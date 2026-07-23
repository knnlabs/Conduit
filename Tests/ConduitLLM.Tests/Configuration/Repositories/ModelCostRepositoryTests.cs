using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Tests.Configuration.Repositories;

[Collection("RepositoryTests")]
public class ModelCostRepositoryTests : RepositoryTestBase
{
    private readonly ModelCostRepository _repository;

    public ModelCostRepositoryTests()
    {
        _repository = new ModelCostRepository(
            CreateDbContextFactory(),
            new LoggerFactory().CreateLogger<ModelCostRepository>());
    }

    [Fact]
    public async Task GetByProviderAsync_ReturnsOnlyCostsMappedToRequestedProvider()
    {
        var seeded = SeedDistinctProviderCosts();

#pragma warning disable CS0618 // The obsolete path remains supported and must preserve provider scoping.
        var firstProviderCosts = await _repository.GetByProviderAsync(seeded.FirstProviderId);
        var secondProviderCosts = await _repository.GetByProviderAsync(seeded.SecondProviderId);
#pragma warning restore CS0618

        Assert.Collection(firstProviderCosts, cost => Assert.Equal(seeded.FirstCostId, cost.Id));
        Assert.Collection(secondProviderCosts, cost => Assert.Equal(seeded.SecondCostId, cost.Id));
    }

    [Fact]
    public async Task GetByProviderPaginatedAsync_ReturnsOnlyCostsMappedToRequestedProvider()
    {
        var seeded = SeedDistinctProviderCosts();

        var firstPage = await _repository.GetByProviderPaginatedAsync(seeded.FirstProviderId, 1, 10);
        var secondPage = await _repository.GetByProviderPaginatedAsync(seeded.SecondProviderId, 1, 10);

        Assert.Equal(1, firstPage.TotalCount);
        Assert.Collection(firstPage.Items, cost => Assert.Equal(seeded.FirstCostId, cost.Id));
        Assert.Equal(1, secondPage.TotalCount);
        Assert.Collection(secondPage.Items, cost => Assert.Equal(seeded.SecondCostId, cost.Id));
    }

    private SeededProviderCosts SeedDistinctProviderCosts()
    {
        using var context = CreateContext();

        var firstProvider = new Provider
        {
            ProviderName = "First OpenAI Provider",
            ProviderType = ProviderType.OpenAI
        };
        var secondProvider = new Provider
        {
            ProviderName = "Second OpenAI Provider",
            ProviderType = ProviderType.OpenAI
        };
        var author = new ModelAuthor { Name = "Provider Scope Test Author" };
        var series = new ModelSeries
        {
            Author = author,
            Name = "Provider Scope Test Series",
            Parameters = "{}"
        };
        var firstModel = new Model { Name = "first-provider-model", Series = series };
        var secondModel = new Model { Name = "second-provider-model", Series = series };
        var firstCost = new ModelCost { CostName = "First Provider Cost" };
        var secondCost = new ModelCost { CostName = "Second Provider Cost" };
        var firstAssociation = new ModelProviderTypeAssociation
        {
            Model = firstModel,
            Identifier = "first-provider-model",
            Provider = ProviderType.OpenAI,
            ModelCost = firstCost,
            IsEnabled = true
        };
        var secondAssociation = new ModelProviderTypeAssociation
        {
            Model = secondModel,
            Identifier = "second-provider-model",
            Provider = ProviderType.OpenAI,
            ModelCost = secondCost,
            IsEnabled = true
        };

        context.ModelProviderMappings.AddRange(
            new ModelProviderMapping
            {
                ModelAlias = "first-provider-model",
                ProviderModelId = "first-provider-model",
                Provider = firstProvider,
                ModelProviderTypeAssociation = firstAssociation
            },
            new ModelProviderMapping
            {
                ModelAlias = "second-provider-model",
                ProviderModelId = "second-provider-model",
                Provider = secondProvider,
                ModelProviderTypeAssociation = secondAssociation
            });
        context.SaveChanges();

        return new SeededProviderCosts(
            firstProvider.Id,
            secondProvider.Id,
            firstCost.Id,
            secondCost.Id);
    }

    private sealed record SeededProviderCosts(
        int FirstProviderId,
        int SecondProviderId,
        int FirstCostId,
        int SecondCostId);
}
