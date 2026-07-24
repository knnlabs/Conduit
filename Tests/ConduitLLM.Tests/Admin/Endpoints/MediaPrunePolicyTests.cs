using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.TestInfrastructure;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Endpoints;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaPrunePolicyTests : IAsyncDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task QueryPruneCandidatesAsync_UsesEachPoliciesRecentAccessWindow()
    {
        var now = DateTime.UtcNow;
        var assignedEligible = Guid.NewGuid();
        var assignedProtected = Guid.NewGuid();
        var defaultProtected = Guid.NewGuid();
        var defaultEligible = Guid.NewGuid();
        var protectionDisabled = Guid.NewGuid();
        await _database.SeedAsync(async context =>
        {
            context.MediaRetentionPolicies.AddRange(
                Policy(1, "assigned", respectRecentAccess: true, windowDays: 5),
                Policy(
                    2,
                    "default",
                    respectRecentAccess: true,
                    windowDays: 20,
                    isDefault: true),
                Policy(3, "unprotected", respectRecentAccess: false, windowDays: 30));
            context.VirtualKeyGroups.AddRange(
                Group(1, policyId: 1),
                Group(2, policyId: null),
                Group(3, policyId: 3));
            context.VirtualKeys.AddRange(
                Key(1, 1),
                Key(2, 2),
                Key(3, 3));
            context.MediaRecords.AddRange(
                Media(
                    assignedEligible,
                    1,
                    "assigned-eligible",
                    now.AddDays(-10),
                    publicUrl: "https://cdn.example.com/assigned-eligible"),
                Media(assignedProtected, 1, "assigned-protected", now.AddDays(-2)),
                Media(defaultProtected, 2, "default-protected", now.AddDays(-10)),
                Media(defaultEligible, 2, "default-eligible", now.AddDays(-30)),
                Media(protectionDisabled, 3, "protection-disabled", now.AddDays(-1)));
            await context.SaveChangesAsync();
        });
        await using var context = _database.CreateContext();

        var candidates = await MediaEndpoints.QueryPruneCandidatesAsync(
            context,
            daysToKeep: 30,
            CancellationToken.None);

        candidates.Select(media => media.Id).Should().BeEquivalentTo(
            new[]
            {
                assignedEligible,
                defaultEligible,
                protectionDisabled
            });
    }

    private static MediaRetentionPolicy Policy(
        int id,
        string name,
        bool respectRecentAccess,
        int windowDays,
        bool isDefault = false) => new()
    {
        Id = id,
        Name = name,
        PositiveBalanceRetentionDays = 60,
        ZeroBalanceRetentionDays = 30,
        NegativeBalanceRetentionDays = 7,
        SoftDeleteGracePeriodDays = 7,
        RespectRecentAccess = respectRecentAccess,
        RecentAccessWindowDays = windowDays,
        IsDefault = isDefault,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static VirtualKeyGroup Group(int id, int? policyId) => new()
    {
        Id = id,
        GroupName = $"group-{id}",
        Balance = 1,
        MediaRetentionPolicyId = policyId,
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
        Guid id,
        int virtualKeyId,
        string storageKey,
        DateTime lastAccessedAt,
        string? publicUrl = null) => new()
    {
        Id = id,
        VirtualKeyId = virtualKeyId,
        StorageKey = storageKey,
        MediaType = "image",
        SizeBytes = 100,
        CreatedAt = DateTime.UtcNow.AddDays(-60),
        LastAccessedAt = lastAccessedAt,
        PublicUrl = publicUrl
    };

    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
