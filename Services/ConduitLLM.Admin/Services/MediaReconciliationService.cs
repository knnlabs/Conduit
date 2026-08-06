using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Metrics;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Finds storage objects that have no matching MediaRecord.
/// </summary>
public sealed class MediaReconciliationService : IMediaReconciliationService
{
    private readonly IMediaStorageService _storageService;
    private readonly IConfigurationDbContext _configurationContext;
    private readonly IMediaDeletionEngine _deletionEngine;
    private readonly IMediaCleanupStatusService _statusService;
    private readonly MediaLifecycleOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MediaReconciliationService> _logger;

    public MediaReconciliationService(
        IMediaStorageService storageService,
        IConfigurationDbContext configurationContext,
        IMediaDeletionEngine deletionEngine,
        IMediaCleanupStatusService statusService,
        IOptions<MediaLifecycleOptions> options,
        ILogger<MediaReconciliationService> logger,
        TimeProvider? timeProvider = null)
    {
        _storageService = storageService;
        _configurationContext = configurationContext;
        _deletionEngine = deletionEngine;
        _statusService = statusService;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<MediaDeletionEngineResult> ReconcileAsync(
        MediaDeletionOperationContext operation,
        CancellationToken cancellationToken = default)
    {
        if (_options.TestVirtualKeyGroups.Count > 0)
        {
            _logger.LogInformation(
                "Skipping storage reconciliation because test virtual key groups are configured and untracked objects cannot be scoped by group");
            return await _deletionEngine.DeleteAsync(
                new MediaDeletionRequest(
                    Array.Empty<MediaRecord>(),
                    operation,
                    StatusOverride: "Skipped: test virtual key group scope is active"),
                cancellationToken);
        }

        var cutoff = _timeProvider.GetUtcNow()
            .Subtract(TimeSpan.FromHours(Math.Max(0, _options.ReconciliationMinimumAgeHours)))
            .UtcDateTime;
        var pageSize = Math.Clamp(_options.ReconciliationPageSize, 1, 1000);
        var seenStorageKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenContinuationTokens = new HashSet<string>(StringComparer.Ordinal);
        var result = MediaDeletionEngineResult.Empty;
        var untrackedCount = 0;
        long untrackedBytes = 0;
        var protectedRecentCount = 0;
        string? continuationToken = null;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _storageService.ListObjectsAsync(
                continuationToken,
                pageSize,
                cancellationToken);
            var objects = page.Objects
                .Where(item => seenStorageKeys.Add(item.StorageKey))
                .ToList();

            if (objects.Count > 0)
            {
                var pageKeys = objects.Select(item => item.StorageKey).ToList();
                // Tombstones still own their storage until the purge phase.
                var trackedKeys = await _configurationContext.MediaRecords
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(record => pageKeys.Contains(record.StorageKey))
                    .Select(record => record.StorageKey)
                    .ToHashSetAsync(cancellationToken);
                var untrackedObjects = objects
                    .Where(item => !trackedKeys.Contains(item.StorageKey))
                    .ToList();
                var eligibleObjects = untrackedObjects
                    .Where(item => item.LastModifiedUtc < cutoff)
                    .ToList();

                untrackedCount += untrackedObjects.Count;
                untrackedBytes += untrackedObjects.Sum(item => item.SizeBytes);
                protectedRecentCount += untrackedObjects.Count - eligibleObjects.Count;

                if (!result.BudgetExhausted && eligibleObjects.Count > 0)
                {
                    var pageResult = await _deletionEngine.DeleteAsync(
                        new MediaDeletionRequest(
                            Array.Empty<MediaRecord>(),
                            operation,
                            UntrackedStorageObjects: eligibleObjects),
                        cancellationToken);
                    result = result.Combine(pageResult);
                }
            }

            continuationToken = page.NextContinuationToken;
            if (!string.IsNullOrEmpty(continuationToken) &&
                !seenContinuationTokens.Add(continuationToken))
            {
                _logger.LogError(
                    "Storage listing returned repeated continuation token {ContinuationToken}; ending reconciliation to avoid an infinite loop",
                    continuationToken);
                result = result with { Failures = result.Failures + 1 };
                break;
            }
        } while (!string.IsNullOrEmpty(continuationToken));

        var remainingCount = Math.Max(0, untrackedCount - result.FilesDeleted);
        var remainingBytes = Math.Max(0, untrackedBytes - result.BytesFreed);
        await _statusService.RecordReconciliationDriftAsync(
            remainingCount,
            remainingBytes,
            cancellationToken);
        AdminMediaCleanupMetrics.UntrackedObjects.Set(remainingCount);
        AdminMediaCleanupMetrics.UntrackedBytes.Set(remainingBytes);

        _logger.LogInformation(
            "Storage reconciliation found {UntrackedCount} untracked objects ({UntrackedBytes} bytes); " +
            "{ProtectedRecentCount} were newer than the {MinimumAgeHours}-hour safety window and " +
            "{RemainingCount} remain after this run",
            untrackedCount,
            untrackedBytes,
            protectedRecentCount,
            _options.ReconciliationMinimumAgeHours,
            remainingCount);

        return result;
    }
}
