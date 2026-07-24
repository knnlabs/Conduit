using System.Diagnostics;
using System.Net;
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
    private readonly IMediaStorageConfigurationGuard _storageGuard;
    private readonly MediaLifecycleOptions _options;
    private readonly ILogger<MediaDeletionEngine> _logger;

    public MediaDeletionEngine(
        IMediaStorageService storageService,
        IMediaDeletionBudgetService budgetService,
        IMediaRecordRepository mediaRepository,
        IMediaCleanupStatusService statusService,
        IMediaStorageConfigurationGuard storageGuard,
        IOptions<MediaLifecycleOptions> options,
        ILogger<MediaDeletionEngine> logger)
    {
        _storageService = storageService;
        _budgetService = budgetService;
        _mediaRepository = mediaRepository;
        _statusService = statusService;
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
        if (!await _storageGuard.ValidateAsync(cancellationToken))
        {
            return new MediaDeletionEngineResult(
                Failures: request.MediaRecords.Count,
                StatusOverride: "Blocked: unsafe storage configuration");
        }

        var processedRecordIds = request.ProcessedRecordIds;
        var uniqueMediaRecords = request.MediaRecords
            .Where(record => processedRecordIds == null || processedRecordIds.Add(record.Id))
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .ToList();

        var isDryRun = _options.DryRunMode && !request.Operation.Force;
        if (uniqueMediaRecords.Count == 0 || !string.IsNullOrWhiteSpace(request.StatusOverride))
        {
            return new MediaDeletionEngineResult(
                StatusOverride: request.StatusOverride,
                IsDryRun: isDryRun);
        }

        if (_options.RequireManualApprovalForLargeBatches &&
            uniqueMediaRecords.Count > _options.LargeBatchThreshold &&
            !isDryRun &&
            !request.Operation.Force)
        {
            _logger.LogWarning(
                "{CleanupType} cleanup batch of {Count} files exceeds threshold of {Threshold}. Manual approval required.",
                request.Operation.CleanupType,
                uniqueMediaRecords.Count,
                _options.LargeBatchThreshold);
            return new MediaDeletionEngineResult(
                StatusOverride: "Skipped: manual approval required",
                IsDryRun: isDryRun);
        }

        if (request.Operation.Force && _options.DryRunMode)
        {
            _logger.LogWarning(
                "Dry-run override requested for {CleanupType} cleanup by {TriggeredBy}",
                request.Operation.CleanupType,
                request.Operation.TriggeredBy);
        }

        var result = MediaDeletionEngineResult.Empty with { IsDryRun = isDryRun };
        foreach (var batch in uniqueMediaRecords.Chunk(_options.MaxBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!isDryRun &&
                await _budgetService.WouldExceedBudgetAsync(
                    batch.Length,
                    _options.MonthlyDeleteBudget,
                    cancellationToken))
            {
                var remaining = await _budgetService.GetRemainingBudgetAsync(
                    _options.MonthlyDeleteBudget,
                    cancellationToken);
                _logger.LogWarning(
                    "Monthly media-delete budget would be exceeded by batch {BatchSize}; {Remaining} deletes remain",
                    batch.Length,
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
        MediaRecord[] batch,
        MediaDeletionRequest request,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var filesDeleted = 0;
        long bytesFreed = 0;
        var failures = 0;
        var wouldDelete = 0;
        long bytesWouldFree = 0;

        await RateLimiter.WaitAsync(cancellationToken);
        try
        {
            foreach (var mediaRecord in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (isDryRun)
                    {
                        wouldDelete++;
                        bytesWouldFree += mediaRecord.SizeBytes ?? 0;
                        _logger.LogDebug(
                            "[DRY RUN] Would delete media {StorageKey}",
                            mediaRecord.StorageKey);
                    }
                    else
                    {
                        var storageDeleted = await DeleteFromStorageAsync(
                            mediaRecord.StorageKey,
                            cancellationToken);
                        if (!storageDeleted)
                        {
                            failures++;
                            continue;
                        }

                        filesDeleted++;
                        bytesFreed += mediaRecord.SizeBytes ?? 0;
                        if (!await _mediaRepository.DeleteAsync(mediaRecord.Id))
                        {
                            failures++;
                            _logger.LogWarning(
                                "Storage object {StorageKey} was deleted but media record {MediaId} could not be removed",
                                mediaRecord.StorageKey,
                                mediaRecord.Id);
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
                        mediaRecord.StorageKey);
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
                        mediaRecord.StorageKey);
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
                filesDeleted,
                bytesFreed,
                failures,
                WouldDeleteCount: wouldDelete,
                BytesWouldFree: bytesWouldFree,
                IsDryRun: isDryRun);
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
    }
}
