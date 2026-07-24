using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class AdminMediaServiceSoftDeleteTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 1, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly ConduitDbContext _context;
    private readonly Mock<IMediaRecordRepository> _repository = new();
    private readonly Mock<IMediaStorageService> _storage = new();
    private readonly Mock<IDistributedLockService> _cleanupLock = new();

    public AdminMediaServiceSoftDeleteTests()
    {
        _context = _database.CreateContext();
        _context.MediaRetentionPolicies.Add(new MediaRetentionPolicy
        {
            Id = 1,
            Name = "assigned-policy",
            PositiveBalanceRetentionDays = 30,
            ZeroBalanceRetentionDays = 30,
            NegativeBalanceRetentionDays = 30,
            SoftDeleteGracePeriodDays = 3,
            IsActive = true,
            CreatedAt = Now.UtcDateTime
        });
        _context.VirtualKeyGroups.Add(new VirtualKeyGroup
        {
            Id = 1,
            GroupName = "soft-delete-group",
            Balance = 1,
            MediaRetentionPolicyId = 1,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        });
        _context.VirtualKeys.Add(new VirtualKey
        {
            Id = 1,
            KeyName = "soft-delete-key",
            KeyHash = "soft-delete-hash",
            VirtualKeyGroupId = 1,
            IsEnabled = true,
            CreatedAt = Now.UtcDateTime
        });
        _context.SaveChanges();
        _cleanupLock.Setup(service => service.AcquireLockAsync(
                MediaCleanupLock.Key,
                MediaCleanupLock.Duration,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDistributedLock>());
    }

    [Fact]
    public async Task DeleteMediaAsync_WithSoftDelete_TombstonesWithoutDeletingStorage()
    {
        var record = CreateRecord();
        _repository.Setup(repository => repository.GetByIdAsync(record.Id))
            .ReturnsAsync(record);
        _repository.Setup(repository => repository.TombstoneAsync(
                record.Id,
                Now.UtcDateTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().DeleteMediaAsync(record.Id);

        result.Should().Be(new AdminMediaDeleteResult(true, Now.UtcDateTime));
        _storage.Verify(storage => storage.DeleteAsync(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.HardDeleteAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2, MediaRestoreOutcome.Restored, true)]
    [InlineData(3, MediaRestoreOutcome.GracePeriodElapsed, false)]
    public async Task RestoreMediaAsync_EnforcesAssignedPolicyGracePeriod(
        int daysSinceDelete,
        MediaRestoreOutcome expected,
        bool shouldRestore)
    {
        var record = CreateRecord();
        record.DeletedAt = Now.UtcDateTime.AddDays(-daysSinceDelete);
        _repository.Setup(repository => repository.GetByIdIncludingDeletedAsync(
                record.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _repository.Setup(repository => repository.RestoreAsync(
                record.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().RestoreMediaAsync(record.Id);

        result.Should().Be(expected);
        _repository.Verify(repository => repository.RestoreAsync(
                record.Id,
                It.IsAny<CancellationToken>()),
            shouldRestore ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task RestoreMediaAsync_WhenCleanupOwnsLock_ReturnsRetryableOutcome()
    {
        _cleanupLock.Setup(service => service.AcquireLockAsync(
                MediaCleanupLock.Key,
                MediaCleanupLock.Duration,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDistributedLock?)null);

        var result = await CreateService().RestoreMediaAsync(Guid.NewGuid());

        result.Should().Be(MediaRestoreOutcome.CleanupInProgress);
        _repository.Verify(repository => repository.GetByIdIncludingDeletedAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private AdminMediaService CreateService() => new(
        _repository.Object,
        Mock.Of<IMediaLifecycleService>(),
        _storage.Object,
        _context,
        _cleanupLock.Object,
        Options.Create(new MediaLifecycleOptions
        {
            EnableSoftDelete = true,
            SoftDeleteGracePeriodDays = 7
        }),
        NullLogger<AdminMediaService>.Instance,
        new FixedTimeProvider(Now));

    private static MediaRecord CreateRecord() => new()
    {
        Id = Guid.NewGuid(),
        VirtualKeyId = 1,
        StorageKey = "soft-delete-media",
        MediaType = "image",
        SizeBytes = 1024,
        CreatedAt = Now.UtcDateTime.AddDays(-10)
    };

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _database.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
