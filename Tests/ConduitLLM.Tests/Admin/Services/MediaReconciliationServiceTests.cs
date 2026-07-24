using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaReconciliationServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly ConduitDbContext _context;
    private readonly Mock<IMediaStorageService> _storage = new();
    private readonly Mock<IMediaRecordRepository> _repository = new();
    private readonly Mock<IMediaDeletionBudgetService> _budget = new();
    private readonly Mock<IMediaCleanupStatusService> _status = new();
    private readonly Mock<IMediaStorageConfigurationGuard> _guard = new();

    public MediaReconciliationServiceTests()
    {
        _context = _database.CreateContext();
        _storage.Setup(service => service.DeleteAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        _repository.Setup(repository => repository.DeleteAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);
        _budget.Setup(service => service.ReserveAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int requested, int _, CancellationToken _) =>
                new MediaDeletionBudgetReservation(requested, requested, requested));
        _guard.Setup(guard => guard.ValidateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ReconcileAsync_DeletesOnlyOldUntrackedObjects()
    {
        SeedTrackedObject("tracked-object");
        _storage.Setup(service => service.ListObjectsAsync(
                null, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaStorageObjectPage
            {
                Objects =
                [
                    StorageObject("tracked-object", 50, Now.AddDays(-10)),
                    StorageObject("old-untracked", 100, Now.AddHours(-49))
                ],
                NextContinuationToken = "next-page"
            });
        _storage.Setup(service => service.ListObjectsAsync(
                "next-page", 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaStorageObjectPage
            {
                Objects =
                [
                    StorageObject("recent-untracked", 200, Now.AddHours(-47))
                ]
            });

        var service = CreateService(new MediaLifecycleOptions
        {
            DryRunMode = false,
            DelayBetweenBatchesMs = 0,
            ReconciliationMinimumAgeHours = 48
        });

        var result = await service.ReconcileAsync(Operation());

        result.FilesDeleted.Should().Be(1);
        result.BytesFreed.Should().Be(100);
        _storage.Verify(storage => storage.DeleteAsync("old-untracked"), Times.Once);
        _storage.Verify(storage => storage.DeleteAsync("recent-untracked"), Times.Never);
        _storage.Verify(storage => storage.DeleteAsync("tracked-object"), Times.Never);
        _repository.Verify(repository => repository.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        _status.Verify(status => status.RecordReconciliationDriftAsync(
            1, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_DryRunReportsDriftWithoutDeleting()
    {
        _storage.Setup(service => service.ListObjectsAsync(
                null, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaStorageObjectPage
            {
                Objects = [StorageObject("old-untracked", 123, Now.AddDays(-3))]
            });

        var service = CreateService(new MediaLifecycleOptions
        {
            DryRunMode = true,
            DelayBetweenBatchesMs = 0,
            ReconciliationMinimumAgeHours = 48
        });

        var result = await service.ReconcileAsync(Operation());

        result.IsDryRun.Should().BeTrue();
        result.WouldDeleteCount.Should().Be(1);
        result.BytesWouldFree.Should().Be(123);
        _storage.Verify(storage => storage.DeleteAsync(It.IsAny<string>()), Times.Never);
        _status.Verify(status => status.RecordReconciliationDriftAsync(
            1, 123, It.IsAny<CancellationToken>()), Times.Once);
    }

    private MediaReconciliationService CreateService(MediaLifecycleOptions options)
    {
        var engine = new MediaDeletionEngine(
            _storage.Object,
            _budget.Object,
            _repository.Object,
            _status.Object,
            Mock.Of<IMediaCleanupApprovalService>(),
            _guard.Object,
            Options.Create(options),
            Mock.Of<ILogger<MediaDeletionEngine>>());
        return new MediaReconciliationService(
            _storage.Object,
            _context,
            engine,
            _status.Object,
            Options.Create(options),
            Mock.Of<ILogger<MediaReconciliationService>>(),
            new FixedTimeProvider(Now));
    }

    private void SeedTrackedObject(string storageKey)
    {
        _context.VirtualKeyGroups.Add(new VirtualKeyGroup
        {
            Id = 1,
            GroupName = "reconciliation-tests",
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        });
        _context.VirtualKeys.Add(new VirtualKey
        {
            Id = 1,
            VirtualKeyGroupId = 1,
            KeyName = "reconciliation-key",
            KeyHash = "reconciliation-key-hash",
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        });
        _context.MediaRecords.Add(new MediaRecord
        {
            Id = Guid.NewGuid(),
            VirtualKeyId = 1,
            StorageKey = storageKey,
            MediaType = "image/png",
            SizeBytes = 50,
            CreatedAt = Now.AddDays(-10).UtcDateTime
        });
        _context.SaveChanges();
    }

    private static MediaStorageObject StorageObject(
        string key,
        long size,
        DateTimeOffset lastModified) => new()
        {
            StorageKey = key,
            SizeBytes = size,
            LastModifiedUtc = lastModified.UtcDateTime
        };

    private static MediaDeletionOperationContext Operation() => new(
        MediaCleanupTypes.Reconciliation,
        "scheduled",
        "test-leader");

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
