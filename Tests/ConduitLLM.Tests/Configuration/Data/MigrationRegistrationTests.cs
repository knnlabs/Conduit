using ConduitLLM.Configuration.Data;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Tests.Configuration.Data;

public sealed class MigrationRegistrationTests
{
    [Fact]
    public void AddDatabaseMigration_DoesNotRegisterSchemaMutationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDatabaseMigration();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(SimpleMigrationService));
    }

    [Theory]
    [InlineData(MigrationMode.Wait, false)]
    [InlineData(MigrationMode.Skip, true)]
    public void ReadinessState_StartsAccordingToRuntimeMode(
        MigrationMode mode,
        bool expectedCurrent)
    {
        var state = new MigrationReadinessState(
            new MigrationStartupOptions { Mode = mode });

        Assert.Equal(expectedCurrent, state.IsSchemaCurrent);
    }
}
