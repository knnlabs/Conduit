using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
                        await AttemptScheduledCleanupAsync(stoppingToken);
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

        private async Task AttemptScheduledCleanupAsync(CancellationToken stoppingToken)
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

            // Get groups to process
            IQueryable<int> groupQuery = context.VirtualKeyGroups.Select(g => g.Id);

            // Filter by test groups if in progressive rollout mode
            if (_options.TestVirtualKeyGroups.Any())
            {
                groupQuery = groupQuery.Where(g => _options.TestVirtualKeyGroups.Contains(g));
                _logger.LogInformation(
                    "Running in test mode - only processing groups: {Groups}",
                    string.Join(", ", _options.TestVirtualKeyGroups));
            }

            var groupIds = await groupQuery.ToListAsync(stoppingToken);

            if (!groupIds.Any())
            {
                _logger.LogDebug("No virtual key groups found to process");
                stopwatch.Stop();
                if (statusService != null)
                {
                    await statusService.RecordRunCompletionAsync(
                        0, 0, stopwatch.Elapsed.TotalSeconds, "No groups", _instanceId, stoppingToken);
                }
                AdminMediaCleanupMetrics.CleanupCycles.WithLabels("no_groups").Inc();
                return;
            }

            _logger.LogInformation(
                "Starting media cleanup for {Count} virtual key groups (DryRun: {DryRun})",
                groupIds.Count, _options.DryRunMode);

            var totalDeleted = 0;
            long totalBytesFreed = 0;

            try
            {
                foreach (var groupId in groupIds)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        status = "Cancelled";
                        break;
                    }

                    var (deleted, bytesFreed) = await ProcessGroupAsync(
                        groupId, context, storageService, budgetService, mediaRepository, stoppingToken);

                    totalDeleted += deleted;
                    totalBytesFreed += bytesFreed;

                    // Small delay between groups
                    await Task.Delay(100, stoppingToken);
                }
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
                    "Media cleanup {Status}. Deleted {Count} files, freed {Bytes:N0} bytes across {Groups} groups in {Duration:F2}s",
                    status, totalDeleted, totalBytesFreed, groupIds.Count, stopwatch.Elapsed.TotalSeconds);

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

                // Record Prometheus metrics
                AdminMediaCleanupMetrics.CleanupDuration.Observe(stopwatch.Elapsed.TotalSeconds);
                AdminMediaCleanupMetrics.GroupsProcessed.Observe(groupIds.Count);
                if (totalDeleted > 0)
                    AdminMediaCleanupMetrics.FilesDeleted.Inc(totalDeleted);
                if (totalBytesFreed > 0)
                    AdminMediaCleanupMetrics.BytesFreed.Inc(totalBytesFreed);
                var cleanupStatus = status.StartsWith("Failed") ? "failed"
                    : status == "Cancelled" ? "cancelled" : "completed";
                AdminMediaCleanupMetrics.CleanupCycles.WithLabels(cleanupStatus).Inc();
            }
        }

        private async Task<(int deleted, long bytesFreed)> ProcessGroupAsync(
            int groupId,
            IConfigurationDbContext context,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
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
                    return (0, 0);
                }

                var retention = await ResolveRetentionSettingsAsync(group, context, stoppingToken);
                if (retention == null)
                    return (0, 0);

                var mediaToDelete = await QueryEligibleMediaAsync(
                    group, retention.Value, context, stoppingToken);
                if (mediaToDelete == null || mediaToDelete.Count == 0)
                    return (0, 0);

                // Process deletions in batches
                return await DeleteMediaBatchesAsync(
                    mediaToDelete, groupId, storageService, budgetService, mediaRepository, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cleanup for group {GroupId}", groupId);
                AdminMediaCleanupMetrics.CleanupErrors.WithLabels("group_processing").Inc();
                return (0, 0);
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

            if (_options.RequireManualApprovalForLargeBatches &&
                mediaToDelete.Count > _options.LargeBatchThreshold)
            {
                _logger.LogWarning(
                    "Batch of {Count} files exceeds threshold of {Threshold}. Manual approval required. Skipping.",
                    mediaToDelete.Count, _options.LargeBatchThreshold);
                return null;
            }

            return mediaToDelete;
        }

        private async Task<(int deleted, long bytesFreed)> DeleteMediaBatchesAsync(
            List<MediaRecord> mediaRecords,
            int groupId,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            CancellationToken stoppingToken)
        {
            var totalDeleted = 0;
            long totalBytesFreed = 0;

            // Process in batches
            var batches = mediaRecords.Chunk(_options.MaxBatchSize);

            foreach (var batch in batches)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                // Check monthly budget before processing
                if (await budgetService.WouldExceedBudgetAsync(batch.Length, _options.MonthlyDeleteBudget, stoppingToken))
                {
                    var remaining = await budgetService.GetRemainingBudgetAsync(_options.MonthlyDeleteBudget, stoppingToken);
                    var currentCount = await budgetService.GetMonthlyDeleteCountAsync(stoppingToken);

                    _logger.LogWarning(
                        "Monthly delete budget would be exceeded. Current: {Current}, Batch: {Batch}, Budget: {Budget}, Remaining: {Remaining}. Stopping cleanup.",
                        currentCount, batch.Length, _options.MonthlyDeleteBudget, remaining);
                    break;
                }

                var (deleted, bytesFreed) = await ProcessBatchAsync(
                    batch, groupId, storageService, budgetService, mediaRepository, stoppingToken);

                totalDeleted += deleted;
                totalBytesFreed += bytesFreed;

                // Delay between batches to avoid overwhelming the system
                if (_options.DelayBetweenBatchesMs > 0)
                {
                    await Task.Delay(_options.DelayBetweenBatchesMs, stoppingToken);
                }
            }

            return (totalDeleted, totalBytesFreed);
        }

        private async Task<(int deleted, long bytesFreed)> ProcessBatchAsync(
            MediaRecord[] batch,
            int groupId,
            IMediaStorageService storageService,
            IMediaDeletionBudgetService budgetService,
            IMediaRecordRepository mediaRepository,
            CancellationToken stoppingToken)
        {
            var successfulDeletes = 0;
            long bytesFreed = 0;

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
                                await mediaRepository.DeleteAsync(mediaRecord.Id);

                                _logger.LogDebug(
                                    "Deleted media record {Id} with storage key {Key}",
                                    mediaRecord.Id, mediaRecord.StorageKey);
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete object {Key}", mediaRecord.StorageKey);
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
                        "Deleted {Count} files, freed {Bytes:N0} bytes for group {GroupId}",
                        successfulDeletes, bytesFreed, groupId);
                }

                return (successfulDeletes, bytesFreed);
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
                await storageService.DeleteAsync(storageKey);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Storage delete operation cancelled for key: {Key}", storageKey);
                return false;
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
