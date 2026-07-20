using System.Reflection;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ConduitLLM.Tests.Configuration.Migrations;

public sealed class ProviderAwareRoutingMigrationTests
{
    [Fact]
    public void Migration_HasDiscoverableMetadataAndTargetModel()
    {
        var migrationType = typeof(AddProviderAwareRouting);

        var migrationAttribute = migrationType.GetCustomAttribute<MigrationAttribute>();
        Assert.NotNull(migrationAttribute);
        Assert.Equal("20260720190000_AddProviderAwareRouting", migrationAttribute.Id);

        var dbContextAttribute = migrationType.GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(dbContextAttribute);
        Assert.Equal(typeof(ConduitDbContext), dbContextAttribute.ContextType);

        var targetModel = new AddProviderAwareRouting().TargetModel;
        var providerMapping = targetModel.FindEntityType(typeof(ModelProviderMapping));

        Assert.NotNull(providerMapping);
        Assert.NotNull(providerMapping.FindProperty(nameof(ModelProviderMapping.RoutingPriority)));
        Assert.NotNull(providerMapping.FindProperty(nameof(ModelProviderMapping.RoutingWeight)));
        Assert.NotNull(targetModel.FindEntityType(typeof(ModelRoutePolicy)));
    }
}
