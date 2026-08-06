using Testcontainers.PostgreSql;
using Xunit;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Shared PostgreSQL container for advisory-lock integration tests.
/// </summary>
public sealed class PostgresLockTestContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("conduit")
        .WithUsername("conduit")
        .WithPassword("conduitpass")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Postgres advisory locks")]
public sealed class PostgresLockCollection :
    ICollectionFixture<PostgresLockTestContainerFixture>;
