using ConduitLLM.Configuration;
using ConduitLLM.Core.Services;
using ConduitLLM.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("Postgres advisory locks")]
[Trait("Category", "Integration")]
[Trait("Component", "DistributedLock")]
public sealed class PostgresDistributedLockServiceTests
{
    private readonly TestDbContextFactory _factory;

    public PostgresDistributedLockServiceTests(
        PostgresLockTestContainerFixture fixture)
    {
        _factory = new TestDbContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Lock_RemainsHeldAcrossDbContextChurn_UntilHandleIsDisposed()
    {
        var firstService = CreateService();
        var secondService = CreateService();
        var firstHandle = await firstService.AcquireLockAsync(
            "media:cleanup:leader",
            TimeSpan.FromMinutes(2));
        firstHandle.Should().NotBeNull();

        (await secondService.AcquireLockAsync(
            "media:cleanup:leader",
            TimeSpan.FromMinutes(2))).Should().BeNull();
        (await secondService.IsLockedAsync("media:cleanup:leader")).Should().BeTrue();

        for (var index = 0; index < 5; index++)
        {
            await using var unrelatedContext = await _factory.CreateDbContextAsync();
            await unrelatedContext.Database.OpenConnectionAsync();
            await unrelatedContext.Database.CloseConnectionAsync();
        }

        (await secondService.AcquireLockAsync(
            "media:cleanup:leader",
            TimeSpan.FromMinutes(2))).Should().BeNull();

        await firstHandle!.DisposeAsync();

        (await secondService.IsLockedAsync("media:cleanup:leader")).Should().BeFalse();
        await using var secondHandle = await secondService.AcquireLockAsync(
            "media:cleanup:leader",
            TimeSpan.FromMinutes(2));
        secondHandle.Should().NotBeNull();
    }

    [Fact]
    public async Task ExpiredHandle_ReleasesSessionLock()
    {
        var firstService = CreateService();
        var secondService = CreateService();
        await using var firstHandle = await firstService.AcquireLockAsync(
            "expiring-lock",
            TimeSpan.FromMilliseconds(300));
        firstHandle.Should().NotBeNull();

        await Task.Delay(TimeSpan.FromMilliseconds(750));

        await using var secondHandle = await secondService.AcquireLockAsync(
            "expiring-lock",
            TimeSpan.FromMinutes(1));
        secondHandle.Should().NotBeNull();
        firstHandle!.IsValid.Should().BeFalse();
    }

    private PostgresDistributedLockService CreateService() =>
        new(
            _factory,
            NullLogger<PostgresDistributedLockService>.Instance);

    private sealed class TestDbContextFactory(string connectionString) :
        IDbContextFactory<ConduitDbContext>
    {
        private readonly DbContextOptions<ConduitDbContext> _options =
            new DbContextOptionsBuilder<ConduitDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        public ConduitDbContext CreateDbContext() => new(_options);

        public Task<ConduitDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
