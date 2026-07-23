using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Admin.Metrics;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Background service for media lifecycle management.
    /// Uses distributed locking to ensure only one instance runs cleanup across a cluster.
    /// Combines scheduling, retention evaluation, and deletion in a single service.
    /// </summary>
    public class MediaCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IDistributedLockService _lockService;
        private readonly MediaLifecycleOptions _options;
        private readonly ILogger<MediaCleanupService> _logger;
        private readonly string _instanceId;

        // Rate limiter for concurrent storage operations
        private static readonly SemaphoreSlim _rateLimiter = new(5, 5);

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCleanupService"/> class.
        /// </summary>
        /// <param name="serviceScopeFactory">Factory for creating service scopes</param>
        /// <param name="lockService">Distributed lock service for leader election</param>
        /// <param name="options">Media lifecycle configuration options</param>
        /// <param name="logger">Logger instance</param>
        public MediaCleanupService(
            IServiceScopeFactory serviceScopeFactory,
            IDistributedLockService lockService,
            IOptions<MediaLifecycleOptions> options,
            ILogger<MediaCleanupService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _lockService = lockService;
            _options = options.Value;
            _logger = logger;
            _instanceId = Guid.NewGuid().ToString("N")[..8];
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.IsSchedulerEnabled)
            {
                _logger.LogInformation(
                    "Media cleanup service disabled for instance {InstanceId}. Set MediaLifecycle:Enabled=true to enable.",
                    _instanceId);
                return;
            }

            _logger.LogInformation(
                "Media cleanup service starting on instance {InstanceId} - DryRun: {DryRun}, Interval: {Interval} minutes",
                _instanceId, _options.DryRunMode, _options.ScheduleIntervalMinutes);

            // Initial delay to let the application fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check runtime toggle before attempting cleanup
                    if (await IsRuntimeEnabledAsync(stoppingToken))
                    {
                        await RunScheduledCleanupAsync(stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Media cleanup service is disabled via runtime toggle on instance {InstanceId}",
                            _instanceId);
                    }

                    await Task.Delay(
                        TimeSpan.FromMinutes(_options.ScheduleIntervalMinutes),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Unexpected error in media cleanup service on instance {InstanceId}",
                        _instanceId);

                    // Wait before retrying to avoid tight error loops
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation(
                "Media cleanup service stopping on instance {InstanceId}",
                _instanceId);
        }

        /// <summary>
        /// Checks if the cleanup service is enabled via runtime toggle.
        /// </summary>
        private async Task<bool> IsRuntimeEnabledAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var statusService = scope.ServiceProvider.GetService<IMediaCleanupStatusService>();
                if (statusService == null)
                {
                    // If status service is not available, fall back to config
                    return _options.IsSchedulerEnabled;
                }
                return await statusService.IsEnabledAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking runtime enabled state, using config default");
                return _options.IsSchedulerEnabled;
            }
        }

        internal async Task RunScheduledCleanupAsync(CancellationToken stoppingToken)
        {
            var lockKey = "media:cleanup:leader";
            var lockDuration = TimeSpan.FromMinutes(30); // Longer lock for actual cleanup work

            using var lockHandle = await _lockService.AcquireLockAsync(
                lockKey,
                lockDuration,
                stoppingToken);

            if (lockHandle == null)
            {
                _logger.LogDebug(
                    "Instance {InstanceId} could not acquire cleanup lock - another instance is leader",
                    _instanceId);
                return;
            }

            _logger.LogInformation(
                "Instance {InstanceId} acquired cleanup leadership",
                _instanceId);

            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            finally
            {
                _logger.LogDebug(
                    "Instance {InstanceId} releasing cleanup leadership",
                    _instanceId);
            }
        }

        private async Task RunCleanupAsync(CancellationToken stoppingToken)
        {
            var stopwatch = Stopwatch.StartNew();
            using var activity = AdminRequestMetrics.StartMediaCleanupActivity(_instanceId, _options.DryRunMode);
            var status = "Completed";

            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();
            var storageService = scope.ServiceProvider.GetRequiredService<IMediaStorageService>();
            var budgetService = scope.ServiceProvider.GetRequiredService<IMediaDeletionBudgetService>();
            var mediaRepository = scope.ServiceProvider.GetRequiredService<IMediaRecordRepository>();
            var statusService = scope.ServiceProvider.GetService<IMediaCleanupStatusService>();
            var totalDeleted = 0;
            long totalBytesFreed = 0;
            var operationFailures = 0;
            var processedRecordIds = new HashSet<Guid>();

            try
            {
                if (_options.TestVirtualKeyGroups.Any())
                {
                    _logger.LogInformation(
                        "Running in test mode - only processing groups: {Groups}",
                        string.Join(", ", _options.TestVirtualKeyGroups));
                }

                if (_options.EnableExpirationCleanup)
                {
                    var result = await ExecuteOperationAsync(
                        MediaCleanupTypes.Expiration,
                        () => ProcessExpiredMediaAsync(
                            context, storageService, budgetService, mediaRepository,
                            processedRecordIds, stoppingToken),
                        statusService,
                        stoppingToken);
                    totalDeleted += result.FilesDeleted;
                    totalBytesFreed += result.BytesFreed;
                    operationFailures += result.Failures;
                }

                if (_options.EnableOrphanCleanup)
                {
                    var result = await ExecuteOperationAsync(
                        MediaCleanupTypes.Orphan,
                        () => ProcessOrphanedMediaAsync(
                            storageService, budgetService, mediaRepository,
                            processedRecordIds, stoppingToken),
                        statusService,
                        stoppingToken);
                    totalDeleted += result.FilesDeleted;
                    totalBytesFreed += result.BytesFreed;
                    operationFailures += result.Failures;
                }

                if (_options.EnableRetentionCleanup)
                {
                    var result = await ExecuteOperationAsync(
                        MediaCleanupTypes.Retention,
                        () => ProcessRetentionMediaAsync(
                            context, storageService, budgetService, mediaRepository,
                            processedRecordIds, stoppingToken),
                        statusService,
                        stoppingToken);
                    totalDeleted += result.FilesDeleted;
                    totalBytesFreed += result.BytesFreed;
                    operationFailures += result.Failures;
                }

                if (operationFailures > 0)
                {
                    status = "Completed with errors";
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                status = "Cancelled";
                throw;
            }
            catch (Exception ex)
            {
                status = $"Failed: {ex.Message}";
                throw;
            }
            finally
            {
                stopwatch.Stop();

                _logger.LogInformation(
                    "Media cleanup {Status}. Deleted {Count} files and freed {Bytes:N0} bytes in {Duration:F2}s",
                    status, totalDeleted, totalBytesFreed, stopwatch.Elapsed.TotalSeconds);

                // Record run completion for status tracking
                if (statusService != null)
                {
                    await statusService.RecordRunCompletionAsync(
                        totalDeleted,
                        totalBytesFreed,
                        stopwatch.Elapsed.TotalSeconds,
                        status,
                        _instanceId,
                        stoppingToken);
                }

            }
        }

        private async Task<CleanupOperationResult> ExecuteOperationAsync(
            string cleanupType,
            Func<Task<CleanupOperationResult>> operation,
            IMediaCleanupStatusService? statusService,
            CancellationToken stoppingToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = CleanupOperationResult.Empty;
            var operationStatus = "Completed";

            try
            {
                result = await operation();
                operationStatus = GetOperationStatus(result);
                return result;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                operationStatus = "Cancelled";
                throw;
            }
            catch (Exception ex)
            {
                operationStatus = $"Failed: {ex.Message}";
                result = new CleanupOperationResult(Failures: 1);
                _logger.LogError(ex, "{CleanupType} media cleanup failed", cleanupType);
                AdminMediaCleanupMetrics.CleanupErrors
                    .WithLabels(cleanupType, "operation")
                    .Inc();
                return result;
            }
            finally
            {
                stopwatch.Stop();

                if (statusService != null)
                {
                    await statusService.RecordOperationCompletionAsync(
                        cleanupType,
                        result.FilesDeleted,
                        result.BytesFreed,
                        stopwatch.Elapsed.TotalSeconds,
                        operationStatus,
                        _instanceId,
                        stoppingToken);
                }

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
                                    : "completed";

                AdminMediaCleanupMetrics.CleanupCycles
                    .WithLabels(cleanupType, metricStatus)
                    .Inc();
                AdminMediaCleanupMetrics.CleanupDuration
                    .WithLabels(cleanupType)
                    .Observe(stopwatch.Elapsed.TotalSeconds);
                AdminMediaCleanupMetrics.LastRunTimestamp
                    .WithLabels(cleanupType)
                    .Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                AdminMediaCleanupMetrics.LastRunSucceeded
                    .WithLabels(cleanupType)
                    .Set(metricStatus == "completed" ? 1 : 0);
                if (result.FilesDeleted > 0)
                {
                    AdminMediaCleanupMetrics.FilesDeleted
                        .WithLabels(cleanupType)
                        .Inc(result.FilesDeleted);
                }
                if (result.BytesFreed > 0)
                {
                    AdminMediaCleanupMetrics.BytesFreed
                        .WithLabels(cleanupType)
                        .Inc(result.BytesFreed);
                }
            }
        }

        private string GetOperationStatus(CleanupOperationResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.StatusOverride))
                return result.StatusOverride;
            if (result.BudgetExhausted)
                return result.Failures > 0
                    ? "Partial: deletion budget exhausted with errors"
                    : "Partial: deletion budget exhausted";
            if (result.Failures > 0)
                return "Completed with errors";
            return _options.DryRunMode ? "Dry run completed" : "Completed";
        }

        private async Task<CleanupOperationResult> ProcessExpiredMediaAsync(
            IConfigurationDbContext context,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            HashSet<Guid> processedRecordIds,
            CancellationToken stoppingToken)
        {
            var now = DateTime.UtcNow;
            var query = context.MediaRecords
                .AsNoTracking()
                .Where(media => media.ExpiresAt != null && media.ExpiresAt <= now);

            if (_options.TestVirtualKeyGroups.Any())
            {
                query = query.Where(media => context.VirtualKeys.Any(key =>
                    key.Id == media.VirtualKeyId &&
                    _options.TestVirtualKeyGroups.Contains(key.VirtualKeyGroupId)));
            }

            var expiredMedia = await query.ToListAsync(stoppingToken);
            _logger.LogInformation(
                "Found {Count} explicitly expired media files eligible for cleanup",
                expiredMedia.Count);

            return await DeleteMediaBatchesAsync(
                expiredMedia,
                MediaCleanupTypes.Expiration,
                groupId: null,
                storageService,
                budgetService,
                mediaRepository,
                processedRecordIds,
                stoppingToken);
        }

        private async Task<CleanupOperationResult> ProcessOrphanedMediaAsync(
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            HashSet<Guid> processedRecordIds,
            CancellationToken stoppingToken)
        {
            if (_options.TestVirtualKeyGroups.Any())
            {
                _logger.LogInformation(
                    "Skipping orphan cleanup because test virtual key groups are configured and orphan ownership cannot be scoped safely");
                return new CleanupOperationResult(
                    StatusOverride: "Skipped: test virtual key group scope is active");
            }

            var orphanedMedia = await mediaRepository.GetOrphanedMediaAsync(stoppingToken);
            _logger.LogInformation(
                "Found {Count} orphaned media files eligible for cleanup",
                orphanedMedia.Count);

            return await DeleteMediaBatchesAsync(
                orphanedMedia,
                MediaCleanupTypes.Orphan,
                groupId: null,
                storageService,
                budgetService,
                mediaRepository,
                processedRecordIds,
                stoppingToken);
        }

        private async Task<CleanupOperationResult> ProcessRetentionMediaAsync(
            IConfigurationDbContext context,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            HashSet<Guid> processedRecordIds,
            CancellationToken stoppingToken)
        {
            IQueryable<int> groupQuery = context.VirtualKeyGroups.Select(group => group.Id);
            if (_options.TestVirtualKeyGroups.Any())
            {
                groupQuery = groupQuery.Where(groupId => _options.TestVirtualKeyGroups.Contains(groupId));
            }

            var groupIds = await groupQuery.ToListAsync(stoppingToken);
            var result = CleanupOperationResult.Empty;

            foreach (var groupId in groupIds)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var groupResult = await ProcessGroupAsync(
                    groupId,
                    context,
                    storageService,
                    budgetService,
                    mediaRepository,
                    processedRecordIds,
                    stoppingToken);
                result = result.Combine(groupResult);

                if (groupResult.BudgetExhausted)
                    break;

                await Task.Delay(100, stoppingToken);
            }

            AdminMediaCleanupMetrics.GroupsProcessed.Observe(groupIds.Count);
            return result;
        }

        private async Task<CleanupOperationResult> ProcessGroupAsync(
            int groupId,
            IConfigurationDbContext context,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            HashSet<Guid> processedRecordIds,
            CancellationToken stoppingToken)
        {
            try
            {
                // Get VirtualKeyGroup with retention policy
                var group = await context.VirtualKeyGroups
                    .Include(g => g.MediaRetentionPolicy)
                    .FirstOrDefaultAsync(g => g.Id == groupId, stoppingToken);

                if (group == null)
                {
                    _logger.LogWarning("VirtualKeyGroup {GroupId} not found", groupId);
                    return CleanupOperationResult.Empty;
                }

                var retention = await ResolveRetentionSettingsAsync(group, context, stoppingToken);
                if (retention == null)
                    return CleanupOperationResult.Empty;

                var mediaToDelete = await QueryEligibleMediaAsync(
                    group, retention.Value, context, stoppingToken);
                if (mediaToDelete == null || mediaToDelete.Count == 0)
                    return CleanupOperationResult.Empty;

                // Process deletions in batches
                return await DeleteMediaBatchesAsync(
                    mediaToDelete,
                    MediaCleanupTypes.Retention,
                    groupId,
                    storageService,
                    budgetService,
                    mediaRepository,
                    processedRecordIds,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cleanup for group {GroupId}", groupId);
                AdminMediaCleanupMetrics.CleanupErrors
                    .WithLabels(MediaCleanupTypes.Retention, "group_processing")
                    .Inc();
                return new CleanupOperationResult(Failures: 1);
            }
        }

        /// <summary>
        /// Resolves retention settings for a group, preferring simple override over policy-based retention.
        /// Returns null if no retention settings can be determined.
        /// </summary>
        private async Task<(int retentionDays, bool respectRecentAccess, int recentAccessWindowDays)?> ResolveRetentionSettingsAsync(
            VirtualKeyGroup group,
            IConfigurationDbContext context,
            CancellationToken stoppingToken)
        {
            var simpleOverride = await GetSimpleRetentionOverrideAsync(stoppingToken);
            if (simpleOverride.HasValue)
            {
                _logger.LogDebug(
                    "Group {GroupId}: Using simple retention override of {Days} days (ignoring balance-based policy)",
                    group.Id, simpleOverride.Value);
                return (simpleOverride.Value, false, 0);
            }

            // No override - use balance-aware policy-based retention
            var policy = group.MediaRetentionPolicy ?? await GetDefaultPolicyAsync(context, stoppingToken);
            if (policy == null)
            {
                _logger.LogDebug(
                    "No retention policy found for group {GroupId} and no default policy exists",
                    group.Id);
                return null;
            }

            var retentionDays = group.Balance switch
            {
                > 0 => policy.PositiveBalanceRetentionDays,
                0 => policy.ZeroBalanceRetentionDays,
                < 0 => policy.NegativeBalanceRetentionDays
            };

            _logger.LogDebug(
                "Group {GroupId} balance: {Balance:C}, retention days: {Days} (policy: {PolicyName})",
                group.Id, group.Balance, retentionDays, policy.Name);

            return (retentionDays, policy.RespectRecentAccess, policy.RecentAccessWindowDays);
        }

        /// <summary>
        /// Queries for media records eligible for cleanup based on retention settings.
        /// Returns null if no eligible media found or if manual approval is required for large batches.
        /// </summary>
        private async Task<List<MediaRecord>?> QueryEligibleMediaAsync(
            VirtualKeyGroup group,
            (int retentionDays, bool respectRecentAccess, int recentAccessWindowDays) retention,
            IConfigurationDbContext context,
            CancellationToken stoppingToken)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retention.retentionDays);

            var virtualKeyIds = await context.VirtualKeys
                .Where(vk => vk.VirtualKeyGroupId == group.Id)
                .Select(vk => vk.Id)
                .ToListAsync(stoppingToken);

            if (!virtualKeyIds.Any())
            {
                _logger.LogDebug("No virtual keys found in group {GroupId}", group.Id);
                return null;
            }

            var mediaToDelete = await context.MediaRecords
                .Where(m => virtualKeyIds.Contains(m.VirtualKeyId))
                .Where(m => m.CreatedAt < cutoffDate)
                .Where(m => !retention.respectRecentAccess ||
                           m.LastAccessedAt == null ||
                           m.LastAccessedAt < DateTime.UtcNow.AddDays(-retention.recentAccessWindowDays))
                .ToListAsync(stoppingToken);

            if (!mediaToDelete.Any())
            {
                _logger.LogDebug("No media eligible for cleanup in group {GroupId}", group.Id);
                return null;
            }

            _logger.LogInformation(
                "Found {Count} media files eligible for cleanup in group {GroupId}",
                mediaToDelete.Count, group.Id);

            return mediaToDelete;
        }

        private async Task<CleanupOperationResult> DeleteMediaBatchesAsync(
            List<MediaRecord> mediaRecords,
            string cleanupType,
            int? groupId,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            HashSet<Guid> processedRecordIds,
            CancellationToken stoppingToken)
        {
            var uniqueMediaRecords = mediaRecords
                .Where(record => processedRecordIds.Add(record.Id))
                .ToList();

            if (uniqueMediaRecords.Count == 0)
                return CleanupOperationResult.Empty;

            if (_options.RequireManualApprovalForLargeBatches &&
                uniqueMediaRecords.Count > _options.LargeBatchThreshold)
            {
                _logger.LogWarning(
                    "{CleanupType} cleanup batch of {Count} files exceeds threshold of {Threshold}. Manual approval required. Skipping.",
                    cleanupType, uniqueMediaRecords.Count, _options.LargeBatchThreshold);
                return new CleanupOperationResult(
                    StatusOverride: "Skipped: manual approval required");
            }

            var result = CleanupOperationResult.Empty;

            // Process in batches
            var batches = uniqueMediaRecords.Chunk(_options.MaxBatchSize);

            foreach (var batch in batches)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                // Check monthly budget before processing
                if (!_options.DryRunMode &&
                    await budgetService.WouldExceedBudgetAsync(batch.Length, _options.MonthlyDeleteBudget, stoppingToken))
                {
                    var remaining = await budgetService.GetRemainingBudgetAsync(_options.MonthlyDeleteBudget, stoppingToken);
                    var currentCount = await budgetService.GetMonthlyDeleteCountAsync(stoppingToken);

                    _logger.LogWarning(
                        "Monthly delete budget would be exceeded. Current: {Current}, Batch: {Batch}, Budget: {Budget}, Remaining: {Remaining}. Stopping cleanup.",
                        currentCount, batch.Length, _options.MonthlyDeleteBudget, remaining);
                    result = result with { BudgetExhausted = true };
                    break;
                }

                var batchResult = await ProcessBatchAsync(
                    batch,
                    cleanupType,
                    groupId,
                    storageService,
                    budgetService,
                    mediaRepository,
                    stoppingToken);

                result = result.Combine(batchResult);

                // Delay between batches to avoid overwhelming the system
                if (_options.DelayBetweenBatchesMs > 0)
                {
                    await Task.Delay(_options.DelayBetweenBatchesMs, stoppingToken);
                }
            }

            return result;
        }

        private async Task<CleanupOperationResult> ProcessBatchAsync(
            MediaRecord[] batch,
            string cleanupType,
            int? groupId,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            CancellationToken stoppingToken)
        {
            var successfulDeletes = 0;
            long bytesFreed = 0;
            var failures = 0;

            await _rateLimiter.WaitAsync(stoppingToken);
            try
            {
                foreach (var mediaRecord in batch)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        if (_options.DryRunMode)
                        {
                            _logger.LogDebug("[DRY RUN] Would delete: {Key}", mediaRecord.StorageKey);
                            successfulDeletes++;
                            bytesFreed += mediaRecord.SizeBytes ?? 0;
                        }
                        else
                        {
                            var deleted = await DeleteFromStorageAsync(storageService, mediaRecord.StorageKey, stoppingToken);

                            if (deleted)
                            {
                                successfulDeletes++;
                                bytesFreed += mediaRecord.SizeBytes ?? 0;

                                // Delete from database
                                var recordDeleted = await mediaRepository.DeleteAsync(mediaRecord.Id);
                                if (!recordDeleted)
                                {
                                    failures++;
                                    _logger.LogWarning(
                                        "Storage object {Key} was deleted but media record {Id} could not be removed",
                                        mediaRecord.StorageKey, mediaRecord.Id);
                                }

                                _logger.LogDebug(
                                    "Deleted media record {Id} with storage key {Key}",
                                    mediaRecord.Id, mediaRecord.StorageKey);
                            }
                            else
                            {
                                failures++;
                            }
                        }

                        // Small delay between deletions to avoid rate limits
                        await Task.Delay(100, stoppingToken);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning(
                            "Rate limit hit while deleting {Key}. Pausing for 5 minutes.",
                            mediaRecord.StorageKey);

                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete object {Key}", mediaRecord.StorageKey);
                        failures++;
                    }
                }

                // Update budget counter if any deletions succeeded
                if (successfulDeletes > 0 && !_options.DryRunMode)
                {
                    var newTotal = await budgetService.IncrementMonthlyDeleteCountAsync(successfulDeletes, stoppingToken);
                    _logger.LogDebug(
                        "Updated monthly deletion count: {Count} (budget: {Budget})",
                        newTotal, _options.MonthlyDeleteBudget);
                }

                if (successfulDeletes > 0)
                {
                    _logger.LogInformation(
                        "{CleanupType} cleanup deleted {Count} files, freed {Bytes:N0} bytes for group {GroupId}",
                        cleanupType, successfulDeletes, bytesFreed, groupId);
                }

                if (failures > 0)
                {
                    AdminMediaCleanupMetrics.CleanupErrors
                        .WithLabels(cleanupType, "storage")
                        .Inc(failures);
                }

                return new CleanupOperationResult(successfulDeletes, bytesFreed, failures);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private async Task<bool> DeleteFromStorageAsync(
            IMediaStorageService storageService,
            string storageKey,
            CancellationToken stoppingToken)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                // Note: IMediaStorageService.DeleteAsync does not accept a CancellationToken,
                // so per-operation timeouts must be enforced by the storage implementation itself.
                return await storageService.DeleteAsync(storageKey);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Storage delete operation cancelled for key: {Key}", storageKey);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting object {Key} from storage", storageKey);
                return false;
            }
        }

        private static async Task<MediaRetentionPolicy?> GetDefaultPolicyAsync(
            IConfigurationDbContext context,
            CancellationToken stoppingToken)
        {
            return await context.MediaRetentionPolicies
                .FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, stoppingToken);
        }

        /// <summary>
        /// Gets the simple retention override if one is set.
        /// Returns null if using policy-based retention.
        /// </summary>
        private async Task<int?> GetSimpleRetentionOverrideAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var statusService = scope.ServiceProvider.GetService<IMediaCleanupStatusService>();
                if (statusService == null)
                {
                    return null;
                }
                return await statusService.GetSimpleRetentionOverrideAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking simple retention override, using policy-based retention");
                return null;
            }
        }

        private sealed record CleanupOperationResult(
            int FilesDeleted = 0,
            long BytesFreed = 0,
            int Failures = 0,
            bool BudgetExhausted = false,
            string? StatusOverride = null)
        {
            public static CleanupOperationResult Empty { get; } = new();

            public CleanupOperationResult Combine(CleanupOperationResult other) => new(
                FilesDeleted + other.FilesDeleted,
                BytesFreed + other.BytesFreed,
                Failures + other.Failures,
                BudgetExhausted || other.BudgetExhausted,
                StatusOverride ?? other.StatusOverride);
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Media cleanup service received stop signal on instance {InstanceId}",
                _instanceId);

            await base.StopAsync(cancellationToken);
        }
    }
}
