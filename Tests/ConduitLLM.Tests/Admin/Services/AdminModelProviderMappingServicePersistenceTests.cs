using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.Helpers;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Admin.Services;

[Collection("RepositoryTests")]
public sealed class AdminModelProviderMappingServicePersistenceTests : RepositoryTestBase
{
    [Fact]
    public async Task AddMappingAsync_PersistsForeignKeysWithoutReinsertingSeedRows()
    {
        ModelMappingSeed seed = null!;
        SeedData(context => seed = ModelMappingTestData.Seed(context));
        var service = CreateService(CreateDbContextFactory());
        var mapping = Mapping(
            "shared-alias",
            seed.OpenAiProviderId,
            ModelMappingSeed.OpenAiModelId,
            seed.OpenAiAssociationId,
            priority: 17,
            weight: 1.25m,
            enabled: true,
            providerOptions: """{"temperature":0.2}""");

        var result = await service.AddMappingAsync(mapping);

        Assert.True(result);
        Assert.True(mapping.Id > 0);
        await using var verification = CreateContext();
        var persisted = await verification.ModelProviderMappings.AsNoTracking().SingleAsync();
        Assert.Equal(mapping.Id, persisted.Id);
        Assert.Equal(seed.OpenAiProviderId, persisted.ProviderId);
        Assert.Equal(seed.OpenAiAssociationId, persisted.ModelProviderTypeAssociationId);
        Assert.Equal(17, persisted.RoutingPriority);
        Assert.Equal(1.25m, persisted.RoutingWeight);
        Assert.Equal("""{"temperature":0.2}""", persisted.ProviderOptions);
        Assert.Equal(3, await verification.Providers.CountAsync());
        Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
    }

    [Fact]
    public async Task UpdateMappingAsync_PersistsEveryDurableFieldAndPreservesRelatedRowsAndCreatedAt()
    {
        ModelMappingSeed seed = null!;
        var createdAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var previousUpdatedAt = createdAt.AddHours(1);
        var mappingId = 0;
        SeedData(context =>
        {
            seed = ModelMappingTestData.Seed(context);
            var mapping = Mapping(
                "before",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId,
                priority: 100,
                weight: 1m,
                enabled: true,
                providerOptions: """{"old":true}""");
            mapping.CreatedAt = createdAt;
            mapping.UpdatedAt = previousUpdatedAt;
            context.ModelProviderMappings.Add(mapping);
            context.SaveChanges();
            mappingId = mapping.Id;
        });
        var service = CreateService(CreateDbContextFactory());

        var result = await service.UpdateMappingAsync(Mapping(
            "after",
            seed.GroqProviderId,
            ModelMappingSeed.GroqModelId,
            seed.GroqAssociationId,
            mappingId,
            priority: 7,
            weight: 1.75m,
            enabled: false,
            providerOptions: """{"route":"fallback"}"""));

        Assert.True(result);
        await using var verification = CreateContext();
        var persisted = await verification.ModelProviderMappings.AsNoTracking().SingleAsync();
        Assert.Equal("after", persisted.ModelAlias);
        Assert.Equal(seed.GroqProviderId, persisted.ProviderId);
        Assert.Equal(ModelMappingSeed.GroqModelId, persisted.ProviderModelId);
        Assert.Equal(seed.GroqAssociationId, persisted.ModelProviderTypeAssociationId);
        Assert.False(persisted.IsEnabled);
        Assert.Equal(7, persisted.RoutingPriority);
        Assert.Equal(1.75m, persisted.RoutingWeight);
        Assert.Equal("""{"route":"fallback"}""", persisted.ProviderOptions);
        Assert.Equal(createdAt, persisted.CreatedAt);
        Assert.True(persisted.UpdatedAt > previousUpdatedAt);
        Assert.Equal(3, await verification.Providers.CountAsync());
        Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
    }

    [Fact]
    public async Task AddMappingAsync_RejectsCaseInsensitiveAliasProviderDuplicateWithoutMutation()
    {
        ModelMappingSeed seed = null!;
        SeedData(context =>
        {
            seed = ModelMappingTestData.Seed(context);
            context.ModelProviderMappings.Add(Mapping(
                "Shared-Alias",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId));
            context.SaveChanges();
        });
        var service = CreateService(CreateDbContextFactory());

        var result = await service.AddMappingAsync(Mapping(
            "shared-alias",
            seed.OpenAiProviderId,
            ModelMappingSeed.OpenAiModelId,
            seed.OpenAiAssociationId));

        Assert.False(result);
        await AssertSingleMappingUnchangedAsync("Shared-Alias", seed.OpenAiProviderId);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("incompatible")]
    [InlineData("weight-low")]
    [InlineData("weight-high")]
    public async Task AddMappingAsync_RejectsInvalidMappingWithoutDurableMutation(string invalidChange)
    {
        ModelMappingSeed seed = null!;
        SeedData(context => seed = ModelMappingTestData.Seed(context));
        var attempted = Mapping(
            "not-created",
            seed.OpenAiProviderId,
            ModelMappingSeed.OpenAiModelId,
            seed.OpenAiAssociationId);
        switch (invalidChange)
        {
            case "disabled":
                attempted.ProviderModelId = ModelMappingSeed.DisabledModelId;
                attempted.ModelProviderTypeAssociationId = seed.DisabledAssociationId;
                break;
            case "incompatible":
                attempted.ProviderModelId = ModelMappingSeed.IncompatibleModelId;
                attempted.ModelProviderTypeAssociationId = seed.IncompatibleAssociationId;
                break;
            case "weight-low":
                attempted.RoutingWeight = 0.09m;
                break;
            case "weight-high":
                attempted.RoutingWeight = 2.01m;
                break;
        }

        var result = await CreateService(CreateDbContextFactory()).AddMappingAsync(attempted);

        Assert.False(result);
        await using var verification = CreateContext();
        Assert.Empty(await verification.ModelProviderMappings.AsNoTracking().ToListAsync());
        Assert.Equal(3, await verification.Providers.CountAsync());
        Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("incompatible")]
    [InlineData("weight-low")]
    [InlineData("weight-high")]
    public async Task UpdateMappingAsync_RejectsInvalidChangesWithoutDurableMutation(string invalidChange)
    {
        ModelMappingSeed seed = null!;
        var mappingId = 0;
        SeedData(context =>
        {
            seed = ModelMappingTestData.Seed(context);
            var mapping = Mapping(
                "unchanged",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId);
            context.ModelProviderMappings.Add(mapping);
            context.SaveChanges();
            mappingId = mapping.Id;
        });
        var attempted = Mapping(
            "changed",
            seed.OpenAiProviderId,
            ModelMappingSeed.OpenAiModelId,
            seed.OpenAiAssociationId,
            mappingId);
        switch (invalidChange)
        {
            case "disabled":
                attempted.ProviderModelId = ModelMappingSeed.DisabledModelId;
                attempted.ModelProviderTypeAssociationId = seed.DisabledAssociationId;
                break;
            case "incompatible":
                attempted.ProviderModelId = ModelMappingSeed.IncompatibleModelId;
                attempted.ModelProviderTypeAssociationId = seed.IncompatibleAssociationId;
                break;
            case "weight-low":
                attempted.RoutingWeight = 0.09m;
                break;
            case "weight-high":
                attempted.RoutingWeight = 2.01m;
                break;
        }

        var result = await CreateService(CreateDbContextFactory()).UpdateMappingAsync(attempted);

        Assert.False(result);
        await AssertSingleMappingUnchangedAsync("unchanged", seed.OpenAiProviderId);
    }

    [Fact]
    public async Task UpdateMappingAsync_RejectsDuplicateAliasProviderWithoutDurableMutation()
    {
        ModelMappingSeed seed = null!;
        var mappingId = 0;
        SeedData(context =>
        {
            seed = ModelMappingTestData.Seed(context);
            var first = Mapping(
                "first",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId);
            var second = Mapping(
                "target",
                seed.SecondOpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId);
            context.ModelProviderMappings.AddRange(first, second);
            context.SaveChanges();
            mappingId = first.Id;
        });

        var result = await CreateService(CreateDbContextFactory()).UpdateMappingAsync(Mapping(
            "TARGET",
            seed.SecondOpenAiProviderId,
            ModelMappingSeed.OpenAiModelId,
            seed.OpenAiAssociationId,
            mappingId));

        Assert.False(result);
        await using var verification = CreateContext();
        var mappings = await verification.ModelProviderMappings.AsNoTracking()
            .OrderBy(mapping => mapping.Id).ToListAsync();
        Assert.Equal(["first", "target"], mappings.Select(mapping => mapping.ModelAlias));
    }

    [Fact]
    public async Task AddMappingAsync_WhenSaveFails_LeavesDurableStateUnchanged()
    {
        var interceptor = new FailNextSaveChangesInterceptor();
        await using var database = new SqliteTestDatabase(interceptor);
        ModelMappingSeed seed = null!;
        database.Seed(context => seed = ModelMappingTestData.Seed(context));
        interceptor.FailNextSave();
        var mapping = Mapping(
            "not-created",
            seed.OpenAiProviderId,
            ModelMappingSeed.OpenAiModelId,
            seed.OpenAiAssociationId);

        var result = await CreateService(database.CreateDbContextFactory()).AddMappingAsync(mapping);

        Assert.False(result);
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.ModelProviderMappings.AsNoTracking().ToListAsync());
        Assert.Equal(3, await verification.Providers.CountAsync());
        Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
    }

    [Fact]
    public async Task UpdateMappingAsync_WhenSaveFails_LeavesDurableStateUnchanged()
    {
        var interceptor = new FailNextSaveChangesInterceptor();
        await using var database = new SqliteTestDatabase(interceptor);
        ModelMappingSeed seed = null!;
        var mappingId = 0;
        database.Seed(context =>
        {
            seed = ModelMappingTestData.Seed(context);
            var mapping = Mapping(
                "unchanged",
                seed.OpenAiProviderId,
                ModelMappingSeed.OpenAiModelId,
                seed.OpenAiAssociationId);
            context.ModelProviderMappings.Add(mapping);
            context.SaveChanges();
            mappingId = mapping.Id;
        });
        interceptor.FailNextSave();

        var result = await CreateService(database.CreateDbContextFactory()).UpdateMappingAsync(Mapping(
            "not-persisted",
            seed.GroqProviderId,
            ModelMappingSeed.GroqModelId,
            seed.GroqAssociationId,
            mappingId,
            weight: 1.5m));

        Assert.False(result);
        await using var verification = database.CreateContext();
        var persisted = await verification.ModelProviderMappings.AsNoTracking().SingleAsync();
        Assert.Equal("unchanged", persisted.ModelAlias);
        Assert.Equal(seed.OpenAiProviderId, persisted.ProviderId);
        Assert.Equal(seed.OpenAiAssociationId, persisted.ModelProviderTypeAssociationId);
        Assert.Equal(3, await verification.Providers.CountAsync());
        Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
    }

    private async Task AssertSingleMappingUnchangedAsync(string alias, int providerId)
    {
        await using var verification = CreateContext();
        var mapping = await verification.ModelProviderMappings.AsNoTracking().SingleAsync();
        Assert.Equal(alias, mapping.ModelAlias);
        Assert.Equal(providerId, mapping.ProviderId);
        Assert.Equal(3, await verification.Providers.CountAsync());
        Assert.Equal(4, await verification.ModelProviderTypeAssociations.CountAsync());
    }

    internal static AdminModelProviderMappingService CreateService(
        IDbContextFactory<ConduitDbContext> factory) =>
        new(
            new ModelProviderMappingRepository(
                factory,
                NullLogger<ModelProviderMappingRepository>.Instance),
            new ProviderRepository(factory, NullLogger<ProviderRepository>.Instance),
            new ModelRepository(factory, NullLogger<ModelRepository>.Instance),
            NullLogger<AdminModelProviderMappingService>.Instance);

    internal static ModelProviderMapping Mapping(
        string alias,
        int providerId,
        string providerModelId,
        int associationId,
        int id = 0,
        int priority = 100,
        decimal weight = 1m,
        bool enabled = true,
        string? providerOptions = null) =>
        new()
        {
            Id = id,
            ModelAlias = alias,
            ProviderId = providerId,
            ProviderModelId = providerModelId,
            ModelProviderTypeAssociationId = associationId,
            RoutingPriority = priority,
            RoutingWeight = weight,
            IsEnabled = enabled,
            ProviderOptions = providerOptions
        };

    private sealed class FailNextSaveChangesInterceptor : SaveChangesInterceptor
    {
        private int _failuresRemaining;

        public void FailNextSave() => _failuresRemaining = 1;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failuresRemaining, 0) == 1)
            {
                throw new DbUpdateException("Injected save failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
