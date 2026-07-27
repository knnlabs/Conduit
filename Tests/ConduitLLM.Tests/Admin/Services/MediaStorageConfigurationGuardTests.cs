using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestInfrastructure;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaStorageConfigurationGuardTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task ValidateAsync_WithPersistedMediaAndInMemoryStorage_BlocksCleanup()
    {
        await using var context = _database.CreateContext();
        SeedMedia(context);
        var logger = new Mock<ILogger<MediaStorageConfigurationGuard>>();
        using var provider = BuildProvider(context);
        var guard = new MediaStorageConfigurationGuard(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryMediaStorageService(Mock.Of<ILogger<InMemoryMediaStorageService>>()),
            logger.Object);

        var allowed = await guard.ValidateAsync();

        allowed.Should().BeFalse();
        guard.IsCleanupAllowed.Should().BeFalse();
        guard.StorageBackend.Should().Be("InMemory");
        logger.Verify(
            entry => entry.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) =>
                    value.ToString()!.Contains("MEDIA CLEANUP BLOCKED")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyDatabaseAndInMemoryStorage_AllowsCleanup()
    {
        await using var context = _database.CreateContext();
        using var provider = BuildProvider(context);
        var guard = new MediaStorageConfigurationGuard(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryMediaStorageService(Mock.Of<ILogger<InMemoryMediaStorageService>>()),
            Mock.Of<ILogger<MediaStorageConfigurationGuard>>());

        var allowed = await guard.ValidateAsync();

        allowed.Should().BeTrue();
    }

    private static ServiceProvider BuildProvider(ConduitDbContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationDbContext>(context);
        return services.BuildServiceProvider();
    }

    private static void SeedMedia(ConduitDbContext context)
    {
        var group = new VirtualKeyGroup
        {
            Id = 1,
            GroupName = "guard-test"
        };
        var key = new VirtualKey
        {
            Id = 1,
            KeyName = "guard-test",
            KeyHash = "hash",
            VirtualKeyGroupId = group.Id,
            VirtualKeyGroup = group
        };
        context.MediaRecords.Add(new MediaRecord
        {
            Id = Guid.NewGuid(),
            StorageKey = "persisted-media",
            VirtualKeyId = key.Id,
            VirtualKey = key,
            MediaType = "image",
            CreatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    public void Dispose() => _database.Dispose();
}
