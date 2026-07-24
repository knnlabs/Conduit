using System.Diagnostics;
using System.Net;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Applies the common safety controls to every tracked-media deletion.
/// </summary>
public sealed class MediaDeletionEngine : IMediaDeletionEngine
{
    private static readonly SemaphoreSlim RateLimiter = new(5, 5);

    private readonly IMediaStorageService _storageService;
    private readonly IMediaDeletionBudgetService _budgetService;
    private readonly IMediaRecordRepository _mediaRepository;
    private readonly IMediaCleanupStatusService _statusService;
    private readonly IMediaCleanupApprovalService _approvalService;
    private readonly IMediaStorageConfigurationGuard _storageGuard;
    private readonly MediaLifecycleOptions _options;
    private readonly ILogger<MediaDeletionEngine> _logger;

    public MediaDeletionEngine(
        IMediaStorageService storageService,
        IMediaDeletionBudgetService budgetService,
        IMediaRecordRepository mediaRepository,
        IMediaCleanupStatusService statusService,
        IMediaCleanupApprovalService approvalService,
        IMediaStorageConfigurationGuard storageGuard,
        IOptions<MediaLifecycleOptions> options,
        ILogger<MediaDeletionEngine> logger)
    {
        _storageService = storageService;
        _budgetService = budgetService;
        _mediaRepository = mediaRepository;
        _statusService = statusService;
        _approvalService = approvalService;
        _storageGuard = storageGuard;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MediaDeletionEngineResult> ExecuteOperationAsync(
        MediaDeletionOperationContext operation,
        Func<Task<MediaDeletionEngineResult>> action,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = MediaDeletionEngineResult.Empty;
        var operationStatus = "Completed";

        try
        {
            result = await action();
            operationStatus = GetOperationStatus(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operationStatus = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            operationStatus = $"Failed: {ex.Message}";
            result = new MediaDeletionEngineResult(Failures: 1);
            _logger.LogError(
                ex,
                "{CleanupType} media cleanup triggered by {TriggeredBy} failed",
                operation.CleanupType, operation.TriggeredBy);
            AdminMediaCleanupMetrics.CleanupErrors
                .WithLabels(operation.CleanupType, "operation")
                .Inc();
        }
        finally
        {
            stopwatch.Stop();
            await _statusService.RecordOperationCompletionAsync(
                operation.CleanupType,
                result.FilesDeleted,
                result.BytesFreed,
                stopwatch.Elapsed.TotalSeconds,
                operationStatus,
                operation.LeaderInstanceId,
                operation.TriggeredBy,
                cancellationToken);

            RecordMetrics(operation, result, operationStatus, stopwatch.Elapsed);
        }

        return result with
        {
            OperationStatus = operationStatus,
            DurationSeconds = stopwatch.Elapsed.TotalSeconds
        };
    }

    /// <inheritdoc />
    public async Task<MediaDeletionEngineResult> DeleteAsync(
        MediaDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var processedRecordIds = request.ProcessedRecordIds;
        var trackedCandidates = request.MediaRecords
            .Where(record => processedRecordIds == null || processedRecordIds.Add(record.Id))
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .Select(record => new DeletionCandidate(
                record.StorageKey,
                record.SizeBytes ?? 0,
                record.Id,
                record.CreatedAt))
            .ToList();
        var trackedStorageKeys = trackedCandidates
            .Select(candidate => candidate.StorageKey)
            .ToHashSet(StringComparer.Ordinal);
        var untrackedCandidates = (request.UntrackedStorageObjects ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.StorageKey))
            .Where(item => !trackedStorageKeys.Contains(item.StorageKey))
            .GroupBy(item => item.StorageKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(item => new DeletionCandidate(
                item.StorageKey,
                item.SizeBytes,
                MediaRecordId: null,
                item.LastModifiedUtc));
        var candidates = trackedCandidates
            .Concat(untrackedCandidates)
            .ToList();

        var isDryRun = _options.DryRunMode && !request.Operation.Force;
        var approvalEligible =
            _options.RequireManualApprovalForLargeBatches &&
            request.Operation.TriggeredBy == "scheduled" &&
            request.Operation.CleanupType != MediaCleanupTypes.VirtualKey &&
            !request.Operation.Force;
        var activeApproval = approvalEligible
            ? await _approvalService.GetActiveApprovalAsync(
                request.Operation.CleanupType,
                request.GroupId,
                cancellationToken)
            : null;
        if (activeApproval != null)
        {
            candidates = candidates
                .Where(candidate => candidate.ScopeTimestampUtc <= activeApproval.CutoffUtc)
                .ToList();
        }

        if (candidates.Count == 0 || !string.IsNullOrWhiteSpace(request.StatusOverride))
        {
            if (candidates.Count == 0 && activeApproval != null)
            {
                await _approvalService.CompleteEmptyApprovalAsync(
                    activeApproval.Id,
                    cancellationToken);
            }

            return new MediaDeletionEngineResult(
                StatusOverride: request.StatusOverride,
                IsDryRun: isDryRun);
        }

        if (candidates.Any(candidate => !ShouldTombstone(candidate, request)) &&
            !await _storageGuard.ValidateAsync(cancellationToken))
        {
            return new MediaDeletionEngineResult(
                Failures: candidates.Count,
                StatusOverride: "Blocked: unsafe storage configuration");
        }

        if (approvalEligible &&
            candidates.Count > _options.LargeBatchThreshold &&
            activeApproval == null)
        {
            await _approvalService.CreateOrRefreshPendingAsync(
                request.Operation.CleanupType,
                request.GroupId,
                candidates.Count,
                candidates.Sum(candidate => candidate.SizeBytes),
                DateTime.UtcNow,
                cancellationToken);
            _logger.LogWarning(
                "{CleanupType} cleanup batch of {Count} files exceeds threshold of {Threshold}. Manual approval required.",
                request.Operation.CleanupType,
                candidates.Count,
                _options.LargeBatchThreshold);
            if (!isDryRun)
            {
                return new MediaDeletionEngineResult(
                    StatusOverride: "Skipped: pending manual approval",
                    IsDryRun: false);
            }
        }

        if (request.Operation.Force && _options.DryRunMode)
        {
            _logger.LogWarning(
                "Dry-run override requested for {CleanupType} cleanup by {TriggeredBy}",
                request.Operation.CleanupType,
                request.Operation.TriggeredBy);
        }

        var result = MediaDeletionEngineResult.Empty with { IsDryRun = isDryRun };
        foreach (var batch in candidates.Chunk(_options.MaxBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var permanentDeleteCount = batch.Count(candidate =>
                !ShouldTombstone(candidate, request));
            if (!isDryRun &&
                permanentDeleteCount > 0 &&
                await _budgetService.WouldExceedBudgetAsync(
                    permanentDeleteCount,
                    _options.MonthlyDeleteBudget,
                    cancellationToken))
            {
                var remaining = await _budgetService.GetRemainingBudgetAsync(
                    _options.MonthlyDeleteBudget,
                    cancellationToken);
                _logger.LogWarning(
                    "Monthly media-delete budget would be exceeded by batch {BatchSize}; {Remaining} deletes remain",
                    permanentDeleteCount,
                    remaining);
                result = result with { BudgetExhausted = true };
                break;
            }

            var batchResult = await ProcessBatchAsync(
                batch,
                request,
                isDryRun,
                cancellationToken);
            result = result.Combine(batchResult);

            if (_options.DelayBetweenBatchesMs > 0)
            {
                await Task.Delay(_options.DelayBetweenBatchesMs, cancellationToken);
            }
        }

        if (activeApproval != null)
        {
            await _approvalService.RecordExecutionAsync(
                activeApproval.Id,
                result,
                cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public MediaDeletionPreview Preview(IEnumerable<MediaRecord> mediaRecords)
    {
        var records = mediaRecords
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .ToList();
        return new MediaDeletionPreview(
            records.Count,
            records.Sum(record => record.SizeBytes ?? 0));
    }

    private async Task<MediaDeletionEngineResult> ProcessBatchAsync(
        DeletionCandidate[] batch,
        MediaDeletionRequest request,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var filesDeleted = 0;
        long bytesFreed = 0;
        var failures = 0;
        var wouldDelete = 0;
        long bytesWouldFree = 0;
        var recordsTombstoned = 0;
        var wouldTombstone = 0;

        await RateLimiter.WaitAsync(cancellationToken);
        try
        {
            foreach (var candidate in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var shouldTombstone = ShouldTombstone(candidate, request);
                    if (isDryRun)
                    {
                        if (shouldTombstone)
                        {
                            wouldTombstone++;
                            _logger.LogDebug(
                                "[DRY RUN] Would tombstone media {StorageKey}",
                                candidate.StorageKey);
                        }
                        else
                        {
                            wouldDelete++;
                            bytesWouldFree += candidate.SizeBytes;
                            _logger.LogDebug(
                                "[DRY RUN] Would permanently delete media {StorageKey}",
                                candidate.StorageKey);
                        }
                    }
                    else if (shouldTombstone)
                    {
                        if (!await _mediaRepository.TombstoneAsync(
                                candidate.MediaRecordId!.Value,
                                DateTime.UtcNow,
                                cancellationToken))
                        {
                            failures++;
                            continue;
                        }

                        recordsTombstoned++;
                    }
                    else
                    {
                        var storageDeleted = await DeleteFromStorageAsync(
                            candidate.StorageKey,
                            cancellationToken);
                        if (!storageDeleted)
                        {
                            failures++;
                            continue;
                        }

                        filesDeleted++;
                        bytesFreed += candidate.SizeBytes;
                        if (candidate.MediaRecordId.HasValue &&
                            !await _mediaRepository.HardDeleteAsync(
                                candidate.MediaRecordId.Value,
                                cancellationToken))
                        {
                            failures++;
                            _logger.LogWarning(
                                "Storage object {StorageKey} was deleted but media record {MediaId} could not be removed",
                                candidate.StorageKey,
                                candidate.MediaRecordId);
                        }
                    }

                    await Task.Delay(100, cancellationToken);
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    failures++;
                    _logger.LogWarning(
                        "Rate limit hit while deleting {StorageKey}; pausing for five minutes",
                        candidate.StorageKey);
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures++;
                    _logger.LogError(
                        ex,
                        "Failed to delete media {StorageKey}",
                        candidate.StorageKey);
                }
            }

            if (filesDeleted > 0)
            {
                var newTotal = await _budgetService.IncrementMonthlyDeleteCountAsync(
                    filesDeleted,
                    cancellationToken);
                _logger.LogInformation(
                    "{CleanupType} cleanup triggered by {TriggeredBy} deleted {Count} files for group {GroupId}; monthly count is {MonthlyCount}",
                    request.Operation.CleanupType,
                    request.Operation.TriggeredBy,
                    filesDeleted,
                    request.GroupId,
                    newTotal);
            }

            if (failures > 0)
            {
                AdminMediaCleanupMetrics.CleanupErrors
                    .WithLabels(request.Operation.CleanupType, "storage")
                    .Inc(failures);
            }

            return new MediaDeletionEngineResult(
                FilesDeleted: filesDeleted,
                BytesFreed: bytesFreed,
                Failures: failures,
                WouldDeleteCount: wouldDelete,
                BytesWouldFree: bytesWouldFree,
                IsDryRun: isDryRun,
                RecordsTombstoned: recordsTombstoned,
                WouldTombstoneCount: wouldTombstone);
        }
        finally
        {
            RateLimiter.Release();
        }
    }

    private async Task<bool> DeleteFromStorageAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _storageService.DeleteAsync(storageKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting media {StorageKey} from storage",
                storageKey);
            return false;
        }
    }

    private bool ShouldTombstone(
        DeletionCandidate candidate,
        MediaDeletionRequest request) =>
        _options.EnableSoftDelete &&
        !request.Purge &&
        request.Operation.CleanupType != MediaCleanupTypes.VirtualKey &&
        candidate.MediaRecordId.HasValue;

    private string GetOperationStatus(MediaDeletionEngineResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StatusOverride))
        {
            return result.StatusOverride;
        }

        if (result.BudgetExhausted)
        {
            return result.Failures > 0
                ? "Partial: deletion budget exhausted with errors"
                : "Partial: deletion budget exhausted";
        }

        if (result.Failures > 0)
        {
            return "Completed with errors";
        }

        return result.IsDryRun ? "Dry run completed" : "Completed";
    }

    private static void RecordMetrics(
        MediaDeletionOperationContext operation,
        MediaDeletionEngineResult result,
        string operationStatus,
        TimeSpan duration)
    {
        var metricStatus = operationStatus.StartsWith("Failed", StringComparison.Ordinal)
            ? "failed"
            : operationStatus == "Cancelled"
                ? "cancelled"
                : operationStatus.Contains("errors", StringComparison.OrdinalIgnoreCase)
                    ? "partial"
                    : operationStatus.Contains("budget", StringComparison.OrdinalIgnoreCase)
                        ? "budget_exhausted"
                        : operationStatus.StartsWith("Skipped", StringComparison.Ordinal)
                            ? "skipped"
                            : result.IsDryRun
                                ? "dry_run"
                                : "completed";

        AdminMediaCleanupMetrics.CleanupCycles
            .WithLabels(operation.CleanupType, metricStatus)
            .Inc();
        AdminMediaCleanupMetrics.CleanupRuns
            .WithLabels(operation.CleanupType, operation.TriggeredBy, metricStatus)
            .Inc();
        AdminMediaCleanupMetrics.CleanupDuration
            .WithLabels(operation.CleanupType)
            .Observe(duration.TotalSeconds);
        AdminMediaCleanupMetrics.LastRunTimestamp
            .WithLabels(operation.CleanupType)
            .Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        AdminMediaCleanupMetrics.LastRunSucceeded
            .WithLabels(operation.CleanupType)
            .Set(metricStatus is "completed" or "dry_run" ? 1 : 0);
        if (result.FilesDeleted > 0)
        {
            AdminMediaCleanupMetrics.FilesDeleted
                .WithLabels(operation.CleanupType)
                .Inc(result.FilesDeleted);
        }
        if (result.BytesFreed > 0)
        {
            AdminMediaCleanupMetrics.BytesFreed
                .WithLabels(operation.CleanupType)
                .Inc(result.BytesFreed);
        }
        if (result.RecordsTombstoned > 0)
        {
            AdminMediaCleanupMetrics.RecordsTombstoned
                .WithLabels(operation.CleanupType)
                .Inc(result.RecordsTombstoned);
        }
    }

    private sealed record DeletionCandidate(
        string StorageKey,
        long SizeBytes,
        Guid? MediaRecordId,
        DateTime ScopeTimestampUtc);
}
