using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaRecordAggregateStatsTests : IAsyncDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task GetAggregateStorageStatsAsync_FiltersGroupAndCapsVirtualKeys()
    {
        await _database.SeedAsync(async context =>
        {
            context.VirtualKeyGroups.AddRange(
                Group(1),
                Group(2));
            context.VirtualKeys.AddRange(
                Key(1, 1),
                Key(2, 1),
                Key(3, 2));
            context.MediaRecords.AddRange(
                Media(1, "group-one-image", "image", "openai", 300),
                Media(2, "group-one-video", "video", "replicate", 500),
                Media(3, "group-two-image", "image", "openai", 1000),
                Media(
                    1,
                    "deleted-group-one",
                    "image",
                    "openai",
                    900,
                    deletedAt: DateTime.UtcNow));
            await context.SaveChangesAsync();
        });
        var repository = new MediaRecordRepository(
            _database.CreateDbContextFactory(),
            NullLogger<MediaRecordRepository>.Instance);

        var result = await repository.GetAggregateStorageStatsAsync(
            virtualKeyGroupId: 1,
            virtualKeyLimit: 1);

        result.TotalFiles.Should().Be(2);
        result.TotalSizeBytes.Should().Be(800);
        result.ByProvider.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["openai"] = 300,
            ["replicate"] = 500
        });
        result.ByMediaType.Should().BeEquivalentTo(
            new[]
            {
                new
                {
                    MediaType = "image",
                    FileCount = 1,
                    SizeBytes = 300L
                },
                new
                {
                    MediaType = "video",
                    FileCount = 1,
                    SizeBytes = 500L
                }
            });
        result.TopVirtualKeys.Should().ContainSingle()
            .Which.VirtualKeyId.Should().Be(2);
    }

    private static VirtualKeyGroup Group(int id) => new()
    {
        Id = id,
        GroupName = $"group-{id}",
        Balance = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static VirtualKey Key(int id, int groupId) => new()
    {
        Id = id,
        KeyName = $"key-{id}",
        KeyHash = $"hash-{id}",
        VirtualKeyGroupId = groupId,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow
    };

    private static MediaRecord Media(
        int virtualKeyId,
        string storageKey,
        string mediaType,
        string provider,
        long sizeBytes,
        DateTime? deletedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        VirtualKeyId = virtualKeyId,
        StorageKey = storageKey,
        MediaType = mediaType,
        Provider = provider,
        SizeBytes = sizeBytes,
        CreatedAt = DateTime.UtcNow,
        DeletedAt = deletedAt
    };

    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
