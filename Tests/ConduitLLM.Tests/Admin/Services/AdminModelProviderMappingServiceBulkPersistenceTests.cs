using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.Helpers;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Admin.Services;

[Collection("RepositoryTests")]
public sealed class AdminModelProviderMappingServiceBulkPersistenceTests : RepositoryTestBase
{
    [Fact]
    public async Task CreateBulkMappingsAsync_WithResolvedDetachedEntities_InsertsOnlyMapping()
    {
        const string providerModelId = "provider/test-model";
        var providerId = 0;
        var associationId = 0;

        SeedData(context =>
        {
            var provider = new Provider
            {
                ProviderName = "Test OpenAI",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };
            var model = ModelTestHelper.CreateCompleteTestModel(providerModelId);

            context.Providers.Add(provider);
            context.Models.Add(model);
            context.SaveChanges();

            var association = new ModelProviderTypeAssociation
            {
                ModelId = model.Id,
                Identifier = providerModelId,
                Provider = ProviderType.OpenAI,
                IsEnabled = true,
                IsPrimary = true
            };
            context.ModelProviderTypeAssociations.Add(association);
            context.SaveChanges();

            providerId = provider.Id;
            associationId = association.Id;
        });

        var factory = CreateDbContextFactory();
        var service = new AdminModelProviderMappingService(
            new ModelProviderMappingRepository(
                factory,
                NullLogger<ModelProviderMappingRepository>.Instance),
            new ProviderRepository(factory, NullLogger<ProviderRepository>.Instance),
            new ModelRepository(factory, NullLogger<ModelRepository>.Instance),
            NullLogger<AdminModelProviderMappingService>.Instance);
        var request = new BulkModelMappingCreateRequest
        {
            Mappings =
            [
                new BulkModelMappingItemDto
                {
                    ModelAlias = "test-alias",
                    ProviderId = providerId,
                    ProviderModelId = providerModelId
                }
            ]
        };

        var result = await service.CreateBulkMappingsAsync(request);

        Assert.True(result.IsSuccess);
        var created = Assert.Single(result.Created);
        Assert.Equal(providerId, created.ProviderId);
        Assert.Equal(associationId, created.ModelProviderTypeAssociationId);
        Assert.NotNull(created.Provider);
        Assert.NotNull(created.Capabilities);

        await using var verificationContext = CreateContext();
        Assert.Equal(1, await verificationContext.Providers.CountAsync());
        Assert.Equal(1, await verificationContext.ModelProviderTypeAssociations.CountAsync());
        var persisted = await verificationContext.ModelProviderMappings.SingleAsync();
        Assert.Equal(providerId, persisted.ProviderId);
        Assert.Equal(associationId, persisted.ModelProviderTypeAssociationId);
    }
}
