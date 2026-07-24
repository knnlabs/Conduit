using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaRecordSoftDeleteTests : IAsyncDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task Repository_DefaultQueriesExcludeTombstonesAndRestoreMakesThemVisible()
    {
        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        await _database.SeedAsync(async context =>
        {
            context.VirtualKeyGroups.Add(new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "soft-delete-tests",
                Balance = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.VirtualKeys.Add(new VirtualKey
            {
                Id = 1,
                KeyName = "soft-delete-key",
                KeyHash = "soft-delete-hash",
                VirtualKeyGroupId = 1,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            context.MediaRecords.AddRange(
                new MediaRecord
                {
                    Id = activeId,
                    VirtualKeyId = 1,
                    StorageKey = "active-media",
                    MediaType = "image",
                    SizeBytes = 100,
                    CreatedAt = DateTime.UtcNow
                },
                new MediaRecord
                {
                    Id = deletedId,
                    VirtualKeyId = 1,
                    StorageKey = "deleted-media",
                    MediaType = "image",
                    SizeBytes = 200,
                    CreatedAt = DateTime.UtcNow,
                    DeletedAt = DateTime.UtcNow.AddDays(-1)
                });
            await context.SaveChangesAsync();
        });
        var repository = CreateRepository();

        (await repository.GetByStorageKeyAsync("deleted-media")).Should().BeNull();
        (await repository.GetByStorageKeyIncludingDeletedAsync("deleted-media"))
            .Should().NotBeNull();
        (await repository.GetByVirtualKeyIdAsync(1))
            .Should().ContainSingle(item => item.Id == activeId);
        (await repository.GetByVirtualKeyIdAsync(1, includeDeleted: true))
            .Should().HaveCount(2);
        (await repository.GetTotalStorageSizeByVirtualKeyAsync(1)).Should().Be(100);

        (await repository.RestoreAsync(deletedId)).Should().BeTrue();
        (await repository.GetByStorageKeyAsync("deleted-media")).Should().NotBeNull();

        (await repository.TombstoneAsync(activeId, DateTime.UtcNow)).Should().BeTrue();
        (await repository.GetByStorageKeyAsync("active-media")).Should().BeNull();
        (await repository.HardDeleteAsync(activeId)).Should().BeTrue();
        (await repository.GetByStorageKeyIncludingDeletedAsync("active-media")).Should().BeNull();
    }

    private MediaRecordRepository CreateRepository() => new(
        _database.CreateDbContextFactory(),
        NullLogger<MediaRecordRepository>.Instance);

    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
