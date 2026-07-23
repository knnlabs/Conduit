using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.Helpers;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class ModelProviderMappingEndpointsTests
{
    [Fact]
    public async Task AuthenticatedPostPutGet_RoundTripsThroughRealRepositories()
    {
        var (host, database, seed) = CreateHost();
        using (host)
        {
            var create = CreateRequest(
                "shared-alias",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId,
                priority: 21,
                weight: 1.1m,
                providerOptions: """{"temperature":0.2}""");

            var post = await host.Client.PostAsJsonAsync("/api/ModelProviderMapping", create);

            Assert.Equal(HttpStatusCode.Created, post.StatusCode);
            var created = Assert.IsType<ModelProviderMappingDto>(
                await post.Content.ReadFromJsonAsync<ModelProviderMappingDto>());
            Assert.Equal($"/api/ModelProviderMapping/{created.Id}", post.Headers.Location?.OriginalString);
            Assert.Equal(create.ModelAlias, created.ModelAlias);
            Assert.Equal(create.ProviderId, created.ProviderId);
            Assert.Equal(create.ModelProviderTypeAssociationId, created.ModelProviderTypeAssociationId);
            Assert.Equal(create.Weight, created.Weight);
            Assert.Equal(create.ProviderOptions, created.ProviderOptions);

            var update = new UpdateModelProviderMappingDto
            {
                ModelAlias = "shared-alias-updated",
                ProviderId = seed.GroqProviderId,
                ProviderModelId = ModelMappingSeed.GroqModelId,
                ModelProviderTypeAssociationId = seed.GroqAssociationId,
                Priority = 3,
                Weight = 1.8m,
                IsEnabled = false,
                ProviderOptions = null
            };
            var put = await host.Client.PutAsJsonAsync(
                $"/api/ModelProviderMapping/{created.Id}", update);

            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

            var get = await host.Client.GetAsync($"/api/ModelProviderMapping/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var response = Assert.IsType<ModelProviderMappingDto>(
                await get.Content.ReadFromJsonAsync<ModelProviderMappingDto>());
            Assert.Equal(update.ModelAlias, response.ModelAlias);
            Assert.Equal(update.ProviderId, response.ProviderId);
            Assert.Equal(update.ProviderModelId, response.ProviderModelId);
            Assert.Equal(update.ModelProviderTypeAssociationId, response.ModelProviderTypeAssociationId);
            Assert.Equal(update.Priority, response.Priority);
            Assert.Equal(update.Weight, response.Weight);
            Assert.Equal(update.IsEnabled, response.IsEnabled);
            Assert.Null(response.ProviderOptions);

            await using var verification = database.CreateContext();
            var persisted = await verification.ModelProviderMappings.AsNoTracking().SingleAsync();
            Assert.Equal(update.ModelAlias, persisted.ModelAlias);
            Assert.Equal(update.ProviderId, persisted.ProviderId);
            Assert.Equal(update.ModelProviderTypeAssociationId, persisted.ModelProviderTypeAssociationId);
            Assert.Equal(update.Weight, persisted.RoutingWeight);
            Assert.Null(persisted.ProviderOptions);
            Assert.Equal(3, await verification.Providers.CountAsync());
            Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
        }
    }

    [Fact]
    public async Task Post_DuplicateAliasProviderConflicts_ButSameAliasDifferentProviderSucceeds()
    {
        var (host, database, seed) = CreateHost();
        using (host)
        {
            var first = await host.Client.PostAsJsonAsync(
                "/api/ModelProviderMapping",
                CreateRequest(
                    "Shared-Alias",
                    seed.OpenAiProviderId,
                    ModelMappingSeed.OpenAiModelId,
                    seed.OpenAiAssociationId));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var duplicate = await host.Client.PostAsJsonAsync(
                "/api/ModelProviderMapping",
                CreateRequest(
                    "shared-alias",
                    seed.OpenAiProviderId,
                    ModelMappingSeed.OpenAiModelId,
                    seed.OpenAiAssociationId));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

            var secondProvider = await host.Client.PostAsJsonAsync(
                "/api/ModelProviderMapping",
                CreateRequest(
                    "shared-alias",
                    seed.GroqProviderId,
                    ModelMappingSeed.GroqModelId,
                    seed.GroqAssociationId));
            Assert.Equal(HttpStatusCode.Created, secondProvider.StatusCode);

            await using var verification = database.CreateContext();
            var mappings = await verification.ModelProviderMappings.AsNoTracking().ToListAsync();
            Assert.Equal(2, mappings.Count);
            Assert.Equal(
                [seed.OpenAiProviderId, seed.GroqProviderId],
                mappings.OrderBy(mapping => mapping.Id).Select(mapping => mapping.ProviderId));
        }
    }

    [Fact]
    public async Task Put_DuplicateAliasProviderConflict_DoesNotMutateEitherMapping()
    {
        var (host, database, seed) = CreateHost();
        using (host)
        {
            var firstResponse = await host.Client.PostAsJsonAsync(
                "/api/ModelProviderMapping",
                CreateRequest(
                    "first",
                    seed.OpenAiProviderId,
                    ModelMappingSeed.OpenAiModelId,
                    seed.OpenAiAssociationId));
            var first = Assert.IsType<ModelProviderMappingDto>(
                await firstResponse.Content.ReadFromJsonAsync<ModelProviderMappingDto>());
            var secondResponse = await host.Client.PostAsJsonAsync(
                "/api/ModelProviderMapping",
                CreateRequest(
                    "target",
                    seed.GroqProviderId,
                    ModelMappingSeed.GroqModelId,
                    seed.GroqAssociationId));
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

            var response = await host.Client.PutAsJsonAsync(
                $"/api/ModelProviderMapping/{first.Id}",
                new UpdateModelProviderMappingDto
                {
                    ModelAlias = "TARGET",
                    ProviderId = seed.GroqProviderId,
                    ProviderModelId = ModelMappingSeed.GroqModelId,
                    ModelProviderTypeAssociationId = seed.GroqAssociationId,
                    Priority = 1,
                    Weight = 1.5m,
                    IsEnabled = false
                });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await using var verification = database.CreateContext();
            var mappings = await verification.ModelProviderMappings.AsNoTracking()
                .OrderBy(mapping => mapping.Id).ToListAsync();
            Assert.Equal(["first", "target"], mappings.Select(mapping => mapping.ModelAlias));
            Assert.All(mappings, mapping => Assert.True(mapping.IsEnabled));
        }
    }

    [Theory]
    [InlineData("{ invalid", "valid JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("""{"MODEL":"override"}""", "reserved key")]
    public async Task Post_InvalidProviderOptions_ReturnsBadRequestWithoutMutation(
        string providerOptions,
        string expectedError)
    {
        var (host, database, seed) = CreateHost();
        using (host)
        {
            var response = await host.Client.PostAsJsonAsync(
                "/api/ModelProviderMapping",
                CreateRequest(
                    "invalid-options",
                    seed.OpenAiProviderId,
                    ModelMappingSeed.OpenAiModelId,
                    seed.OpenAiAssociationId,
                    providerOptions: providerOptions));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(expectedError, await response.Content.ReadAsStringAsync(),
                StringComparison.OrdinalIgnoreCase);
            await using var verification = database.CreateContext();
            Assert.Empty(await verification.ModelProviderMappings.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task Post_DataAnnotationValidationFailure_ReturnsBadRequestWithoutMutation()
    {
        var (host, database, seed) = CreateHost();
        using (host)
        {
            var invalid = CreateRequest(
                "invalid-weight",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId,
                weight: 0.09m);

            var response = await host.Client.PostAsJsonAsync("/api/ModelProviderMapping", invalid);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await using var verification = database.CreateContext();
            Assert.Empty(await verification.ModelProviderMappings.AsNoTracking().ToListAsync());
        }
    }

    private static (AdminEndpointTestHost Host, SqliteTestDatabase Database, ModelMappingSeed Seed)
        CreateHost()
    {
        var database = new SqliteTestDatabase();
        ModelMappingSeed seed = null!;
        database.Seed(context => seed = ModelMappingTestData.Seed(context));
        var factory = database.CreateDbContextFactory();
        var mappingService = new AdminModelProviderMappingService(
            new ModelProviderMappingRepository(
                factory,
                NullLogger<ModelProviderMappingRepository>.Instance),
            new ProviderRepository(factory, NullLogger<ProviderRepository>.Instance),
            new ModelRepository(factory, NullLogger<ModelRepository>.Instance),
            NullLogger<AdminModelProviderMappingService>.Instance);
        var providerService = new Mock<IProviderService>();

        var host = AdminEndpointTestHost.Create(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<IAdminModelProviderMappingService>(mappingService);
            services.AddSingleton(providerService.Object);
            services.AddScoped<ModelProviderMappingEndpoints>();
        }, endpoints => ModelProviderMappingEndpoints.MapModelProviderMappingEndpoints(endpoints), database);
        return (host, database, seed);
    }

    private static CreateModelProviderMappingDto CreateRequest(
        string alias,
        int providerId,
        string providerModelId,
        int associationId,
        int priority = 100,
        decimal weight = 1m,
        string? providerOptions = null) =>
        new()
        {
            ModelAlias = alias,
            ProviderId = providerId,
            ProviderModelId = providerModelId,
            ModelProviderTypeAssociationId = associationId,
            Priority = priority,
            Weight = weight,
            IsEnabled = true,
            ProviderOptions = providerOptions
        };
}
