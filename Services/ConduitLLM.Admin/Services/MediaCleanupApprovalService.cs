using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// EF-backed cleanup approval workflow. Approved scopes are consumed by fresh scheduler queries.
/// </summary>
public sealed class MediaCleanupApprovalService : IMediaCleanupApprovalService
{
    private readonly IConfigurationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly MediaLifecycleOptions _options;

    public MediaCleanupApprovalService(
        IConfigurationDbContext context,
        IOptions<MediaLifecycleOptions> options,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MediaCleanupApproval?> GetActiveApprovalAsync(
        string cleanupType,
        int? virtualKeyGroupId,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var approval = await ScopeQuery(cleanupType, virtualKeyGroupId)
            .Where(item => item.Status == MediaCleanupApprovalStatuses.Approved)
            .OrderByDescending(item => item.DecisionAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (approval == null)
        {
            return null;
        }

        if (approval.ExpiresAtUtc > now)
        {
            return approval;
        }

        approval.Status = MediaCleanupApprovalStatuses.Pending;
        approval.DecisionAtUtc = null;
        approval.DecidedBy = null;
        approval.UpdatedAtUtc = now;
        approval.ExpiresAtUtc = ExpiresAt(now);
        approval.ExecutionStatus = "Approval expired; fresh review required";
        await _context.SaveChangesAsync(cancellationToken);
        await UpdatePendingMetricAsync(cancellationToken);
        return null;
    }

    public async Task<MediaCleanupApproval> CreateOrRefreshPendingAsync(
        string cleanupType,
        int? virtualKeyGroupId,
        int candidateCount,
        long candidateBytes,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var approval = await ScopeQuery(cleanupType, virtualKeyGroupId)
            .Where(item =>
                item.Status == MediaCleanupApprovalStatuses.Pending ||
                item.Status == MediaCleanupApprovalStatuses.Rejected)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (approval == null)
        {
            approval = new MediaCleanupApproval
            {
                Id = Guid.NewGuid(),
                CleanupType = cleanupType,
                VirtualKeyGroupId = virtualKeyGroupId,
                CreatedAtUtc = now
            };
            _context.MediaCleanupApprovals.Add(approval);
        }

        approval.CandidateCount = candidateCount;
        approval.CandidateBytes = candidateBytes;
        approval.CutoffUtc = cutoffUtc;
        approval.Status = MediaCleanupApprovalStatuses.Pending;
        approval.UpdatedAtUtc = now;
        approval.ExpiresAtUtc = ExpiresAt(now);
        approval.DecisionAtUtc = null;
        approval.DecidedBy = null;
        approval.ExecutionStatus = null;
        await _context.SaveChangesAsync(cancellationToken);
        await UpdatePendingMetricAsync(cancellationToken);
        return approval;
    }

    public async Task<IReadOnlyList<MediaCleanupApprovalDto>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var approvals = await _context.MediaCleanupApprovals
            .AsNoTracking()
            .Where(item =>
                item.Status == MediaCleanupApprovalStatuses.Pending &&
                item.ExpiresAtUtc > now)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        AdminMediaCleanupMetrics.PendingApprovals.Set(approvals.Count);
        return approvals.Select(MediaCleanupApprovalDto.FromEntity).ToList();
    }

    public Task<MediaCleanupApproval?> ApproveAsync(
        Guid id,
        string decidedBy,
        CancellationToken cancellationToken = default) =>
        DecideAsync(id, MediaCleanupApprovalStatuses.Approved, decidedBy, cancellationToken);

    public Task<MediaCleanupApproval?> RejectAsync(
        Guid id,
        string decidedBy,
        CancellationToken cancellationToken = default) =>
        DecideAsync(id, MediaCleanupApprovalStatuses.Rejected, decidedBy, cancellationToken);

    public async Task RecordExecutionAsync(
        Guid id,
        MediaDeletionEngineResult result,
        bool isFinalPage = true,
        CancellationToken cancellationToken = default)
    {
        var approval = await _context.MediaCleanupApprovals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval == null)
        {
            return;
        }

        var completed =
            isFinalPage &&
            !result.BudgetExhausted &&
            result.Failures == 0;
        approval.Status = completed
            ? MediaCleanupApprovalStatuses.Completed
            : MediaCleanupApprovalStatuses.Approved;
        approval.ExecutedAtUtc = UtcNow();
        approval.UpdatedAtUtc = approval.ExecutedAtUtc.Value;
        approval.ExecutionStatus = completed
            ? result.IsDryRun ? "Dry run completed" : "Completed"
            : result.BudgetExhausted
                ? "Partially executed: deletion budget exhausted"
                : result.Failures > 0
                    ? "Partially executed with errors; queued for retry"
                    : "Partially executed: more paged candidates remain";
        approval.FilesDeleted += result.FilesDeleted;
        approval.RecordsTombstoned += result.RecordsTombstoned;
        approval.Failures += result.Failures;
        await _context.SaveChangesAsync(cancellationToken);
        await UpdatePendingMetricAsync(cancellationToken);
    }

    public async Task CompleteEmptyApprovalAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var approval = await _context.MediaCleanupApprovals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval == null)
        {
            return;
        }

        approval.Status = MediaCleanupApprovalStatuses.Completed;
        approval.ExecutedAtUtc = UtcNow();
        approval.UpdatedAtUtc = approval.ExecutedAtUtc.Value;
        approval.ExecutionStatus = "Completed: no candidates remain eligible";
        await _context.SaveChangesAsync(cancellationToken);
        await UpdatePendingMetricAsync(cancellationToken);
    }

    private async Task<MediaCleanupApproval?> DecideAsync(
        Guid id,
        string status,
        string decidedBy,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var approval = await _context.MediaCleanupApprovals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval == null ||
            approval.Status != MediaCleanupApprovalStatuses.Pending ||
            approval.ExpiresAtUtc <= now)
        {
            return null;
        }

        approval.Status = status;
        approval.DecisionAtUtc = now;
        approval.DecidedBy = decidedBy;
        approval.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);
        await UpdatePendingMetricAsync(cancellationToken);
        return approval;
    }

    private IQueryable<MediaCleanupApproval> ScopeQuery(
        string cleanupType,
        int? virtualKeyGroupId) =>
        _context.MediaCleanupApprovals.Where(item =>
            item.CleanupType == cleanupType &&
            item.VirtualKeyGroupId == virtualKeyGroupId);

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private DateTime ExpiresAt(DateTime now) =>
        now.AddHours(Math.Max(1, _options.LargeBatchApprovalExpirationHours));

    private async Task UpdatePendingMetricAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var count = await _context.MediaCleanupApprovals.CountAsync(
            item =>
                item.Status == MediaCleanupApprovalStatuses.Pending &&
                item.ExpiresAtUtc > now,
            cancellationToken);
        AdminMediaCleanupMetrics.PendingApprovals.Set(count);
    }
}
