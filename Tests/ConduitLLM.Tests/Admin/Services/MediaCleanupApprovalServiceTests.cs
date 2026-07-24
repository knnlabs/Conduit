using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaCleanupApprovalServiceTests : IDisposable
{
    private readonly ConduitDbContext _context;
    private readonly MediaCleanupApprovalService _service;
    private readonly SqliteTestDatabase _database;

    public MediaCleanupApprovalServiceTests()
    {
        _database = new SqliteTestDatabase();
        _context = _database.CreateContext();
        _service = new MediaCleanupApprovalService(
            _context,
            Options.Create(new MediaLifecycleOptions
            {
                LargeBatchApprovalExpirationHours = 24
            }));
    }

    [Fact]
    public async Task RejectThenNextEvaluation_ReopensFreshPendingSnapshot()
    {
        var initial = await _service.CreateOrRefreshPendingAsync(
            "retention", 7, 101, 1_000, DateTime.UtcNow);
        var rejected = await _service.RejectAsync(initial.Id, "admin");

        rejected!.Status.Should().Be(MediaCleanupApprovalStatuses.Rejected);

        var refreshed = await _service.CreateOrRefreshPendingAsync(
            "retention", 7, 140, 2_000, DateTime.UtcNow.AddMinutes(5));

        refreshed.Id.Should().Be(initial.Id);
        refreshed.Status.Should().Be(MediaCleanupApprovalStatuses.Pending);
        refreshed.CandidateCount.Should().Be(140);
        refreshed.CandidateBytes.Should().Be(2_000);
    }

    [Fact]
    public async Task SuccessfulExecution_CompletesApprovedScope()
    {
        var pending = await _service.CreateOrRefreshPendingAsync(
            "expiration", null, 120, 4_000, DateTime.UtcNow);
        await _service.ApproveAsync(pending.Id, "admin");

        await _service.RecordExecutionAsync(
            pending.Id,
            new MediaDeletionEngineResult(FilesDeleted: 120, BytesFreed: 4_000));

        var stored = await _context.MediaCleanupApprovals.SingleAsync();
        stored.Status.Should().Be(MediaCleanupApprovalStatuses.Completed);
        stored.FilesDeleted.Should().Be(120);
    }

    [Fact]
    public async Task PartialExecution_LeavesApprovalQueuedForRetry()
    {
        var pending = await _service.CreateOrRefreshPendingAsync(
            "purge", null, 120, 4_000, DateTime.UtcNow);
        await _service.ApproveAsync(pending.Id, "admin");

        await _service.RecordExecutionAsync(
            pending.Id,
            new MediaDeletionEngineResult(
                FilesDeleted: 50,
                BytesFreed: 2_000,
                BudgetExhausted: true));

        var stored = await _context.MediaCleanupApprovals.SingleAsync();
        stored.Status.Should().Be(MediaCleanupApprovalStatuses.Approved);
        stored.FilesDeleted.Should().Be(50);
    }

    [Fact]
    public async Task NonFinalPagedExecution_KeepsApprovalActiveUntilFinalPage()
    {
        var pending = await _service.CreateOrRefreshPendingAsync(
            "expiration", null, 120, 4_000, DateTime.UtcNow);
        await _service.ApproveAsync(pending.Id, "admin");

        await _service.RecordExecutionAsync(
            pending.Id,
            new MediaDeletionEngineResult(FilesDeleted: 50, BytesFreed: 2_000),
            isFinalPage: false);

        var afterFirstPage = await _context.MediaCleanupApprovals.SingleAsync();
        afterFirstPage.Status.Should().Be(MediaCleanupApprovalStatuses.Approved);
        afterFirstPage.ExecutionStatus.Should().Contain("paged candidates remain");

        await _service.RecordExecutionAsync(
            pending.Id,
            new MediaDeletionEngineResult(FilesDeleted: 70, BytesFreed: 2_000),
            isFinalPage: true);

        var completed = await _context.MediaCleanupApprovals.SingleAsync();
        completed.Status.Should().Be(MediaCleanupApprovalStatuses.Completed);
        completed.FilesDeleted.Should().Be(120);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}
