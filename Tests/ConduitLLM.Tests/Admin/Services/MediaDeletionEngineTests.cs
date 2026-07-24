using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaDeletionEngineTests
{
    private readonly Mock<IMediaStorageService> _storage = new();
    private readonly Mock<IMediaDeletionBudgetService> _budget = new();
    private readonly Mock<IMediaRecordRepository> _repository = new();
    private readonly Mock<IMediaCleanupStatusService> _status = new();
    private readonly Mock<IMediaStorageConfigurationGuard> _storageGuard = new();

    public MediaDeletionEngineTests()
    {
        _storageGuard
            .Setup(guard => guard.ValidateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _storage
            .Setup(storage => storage.DeleteAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        _repository
            .Setup(repository => repository.HardDeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository
            .Setup(repository => repository.TombstoneAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _budget
            .Setup(budget => budget.WouldExceedBudgetAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _budget
            .Setup(budget => budget.IncrementMonthlyDeleteCountAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task DeleteAsync_DryRun_ReportsScopeWithoutMutatingStorageOrBudget()
    {
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            DryRunMode = true,
            EnableSoftDelete = false,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });
        var records = CreateRecords(2);

        var result = await engine.DeleteAsync(new MediaDeletionRequest(
            records,
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Retention, "manual", "test")));

        result.Should().BeEquivalentTo(new
        {
            FilesDeleted = 0,
            BytesFreed = 0L,
            Failures = 0,
            WouldDeleteCount = 2,
            BytesWouldFree = 300L,
            IsDryRun = true
        });
        _storage.Verify(storage => storage.DeleteAsync(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.HardDeleteAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _budget.Verify(budget => budget.WouldExceedBudgetAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ForceOverride_DeletesAndPreservesRecordWhenStorageFails()
    {
        var records = CreateRecords(2);
        _storage
            .Setup(storage => storage.DeleteAsync(records[1].StorageKey))
            .ReturnsAsync(false);
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            DryRunMode = true,
            EnableSoftDelete = false,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });
        var operation = new MediaDeletionOperationContext(
            MediaCleanupTypes.Retention, "manual", "test", Force: true);

        var result = await engine.DeleteAsync(new MediaDeletionRequest(records, operation));

        result.FilesDeleted.Should().Be(1);
        result.BytesFreed.Should().Be(100);
        result.Failures.Should().Be(1);
        result.IsDryRun.Should().BeFalse();
        _repository.Verify(repository => repository.HardDeleteAsync(
            records[0].Id, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(repository => repository.HardDeleteAsync(
            records[1].Id, It.IsAny<CancellationToken>()), Times.Never);
        _budget.Verify(budget => budget.IncrementMonthlyDeleteCountAsync(
            1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenBudgetWouldBeExceeded_StopsBeforeDeletion()
    {
        _budget
            .Setup(budget => budget.WouldExceedBudgetAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _budget
            .Setup(budget => budget.GetRemainingBudgetAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            DryRunMode = false,
            EnableSoftDelete = false,
            MonthlyDeleteBudget = 50,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });

        var result = await engine.DeleteAsync(new MediaDeletionRequest(
            CreateRecords(2),
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Expiration, "scheduled", "test")));

        result.BudgetExhausted.Should().BeTrue();
        result.FilesDeleted.Should().Be(0);
        _storage.Verify(storage => storage.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_LargeBatch_DryRunAndExplicitForceAreAllowed()
    {
        var records = CreateRecords(2);
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            DryRunMode = true,
            EnableSoftDelete = false,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = true,
            LargeBatchThreshold = 1
        });

        var preview = await engine.DeleteAsync(new MediaDeletionRequest(
            records,
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Retention, "manual", "preview")));
        var forced = await engine.DeleteAsync(new MediaDeletionRequest(
            records,
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Retention, "manual", "force", Force: true)));

        preview.WouldDeleteCount.Should().Be(2);
        forced.FilesDeleted.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_SoftDelete_TombstonesWithoutStorageOrBudget()
    {
        var records = CreateRecords(2);
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            EnableSoftDelete = true,
            DryRunMode = false,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });

        var result = await engine.DeleteAsync(new MediaDeletionRequest(
            records,
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Retention, "scheduled", "test")));

        result.RecordsTombstoned.Should().Be(2);
        result.FilesDeleted.Should().Be(0);
        result.BytesFreed.Should().Be(0);
        _repository.Verify(repository => repository.TombstoneAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _storage.Verify(storage => storage.DeleteAsync(It.IsAny<string>()), Times.Never);
        _budget.Verify(budget => budget.WouldExceedBudgetAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _budget.Verify(budget => budget.IncrementMonthlyDeleteCountAsync(
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeleteDryRun_ReportsTombstonesWithoutFreedBytes()
    {
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            EnableSoftDelete = true,
            DryRunMode = true,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });

        var result = await engine.DeleteAsync(new MediaDeletionRequest(
            CreateRecords(2),
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Expiration, "manual", "test")));

        result.WouldTombstoneCount.Should().Be(2);
        result.WouldDeleteCount.Should().Be(0);
        result.BytesWouldFree.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_Purge_PermanentlyDeletesAndConsumesBudget()
    {
        var records = CreateRecords(2);
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            EnableSoftDelete = true,
            DryRunMode = false,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });

        var result = await engine.DeleteAsync(new MediaDeletionRequest(
            records,
            new MediaDeletionOperationContext(
                MediaCleanupTypes.Purge, "scheduled", "test"),
            Purge: true));

        result.FilesDeleted.Should().Be(2);
        result.BytesFreed.Should().Be(300);
        result.RecordsTombstoned.Should().Be(0);
        _storage.Verify(storage => storage.DeleteAsync(It.IsAny<string>()),
            Times.Exactly(2));
        _repository.Verify(repository => repository.HardDeleteAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _budget.Verify(budget => budget.IncrementMonthlyDeleteCountAsync(
            2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_VirtualKeyRemoval_PermanentlyDeletesEvenWithSoftDeleteEnabled()
    {
        var record = CreateRecords(1);
        var engine = CreateEngine(new MediaLifecycleOptions
        {
            EnableSoftDelete = true,
            DryRunMode = false,
            MaxBatchSize = 10,
            RequireManualApprovalForLargeBatches = false
        });

        var result = await engine.DeleteAsync(new MediaDeletionRequest(
            record,
            new MediaDeletionOperationContext(
                MediaCleanupTypes.VirtualKey, "system", "test")));

        result.FilesDeleted.Should().Be(1);
        result.RecordsTombstoned.Should().Be(0);
        _repository.Verify(repository => repository.HardDeleteAsync(
            record[0].Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteOperationAsync_RecordsManualTriggerAndOutcome()
    {
        var engine = CreateEngine(new MediaLifecycleOptions());
        var operation = new MediaDeletionOperationContext(
            MediaCleanupTypes.Reconciliation, "manual", "manual:test");

        var result = await engine.ExecuteOperationAsync(
            operation,
            () => Task.FromResult(new MediaDeletionEngineResult(
                FilesDeleted: 3,
                BytesFreed: 900)));

        result.OperationStatus.Should().Be("Completed");
        _status.Verify(status => status.RecordOperationCompletionAsync(
            MediaCleanupTypes.Reconciliation,
            3,
            900,
            It.IsAny<double>(),
            "Completed",
            "manual:test",
            "manual",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private MediaDeletionEngine CreateEngine(MediaLifecycleOptions options) => new(
        _storage.Object,
        _budget.Object,
        _repository.Object,
        _status.Object,
        _storageGuard.Object,
        Options.Create(options),
        Mock.Of<ILogger<MediaDeletionEngine>>());

    private static List<MediaRecord> CreateRecords(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new MediaRecord
            {
                Id = Guid.NewGuid(),
                VirtualKeyId = 1,
                StorageKey = $"media-{index}",
                MediaType = "image",
                SizeBytes = index * 100
            })
            .ToList();
}
