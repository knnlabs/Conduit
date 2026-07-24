using System.Diagnostics;
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
        private readonly IMediaStorageConfigurationGuard? _storageConfigurationGuard;
        private readonly string _instanceId;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCleanupService"/> class.
        /// </summary>
        /// <param name="serviceScopeFactory">Factory for creating service scopes</param>
        /// <param name="lockService">Distributed lock service for leader election</param>
        /// <param name="options">Media lifecycle configuration options</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="storageConfigurationGuard">Storage safety guard</param>
        public MediaCleanupService(
            IServiceScopeFactory serviceScopeFactory,
            IDistributedLockService lockService,
            IOptions<MediaLifecycleOptions> options,
            ILogger<MediaCleanupService> logger,
            IMediaStorageConfigurationGuard? storageConfigurationGuard = null)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _lockService = lockService;
            _options = options.Value;
            _logger = logger;
            _storageConfigurationGuard = storageConfigurationGuard;
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
            if (_storageConfigurationGuard != null &&
                !await _storageConfigurationGuard.ValidateAsync(stoppingToken))
            {
                _logger.LogCritical(
                    "Instance {InstanceId} refused to run media cleanup because the storage configuration is unsafe.",
                    _instanceId);
                return;
            }

            using var lockHandle = await _lockService.AcquireLockAsync(
                MediaCleanupLock.Key,
                MediaCleanupLock.Duration,
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
            var deletionEngine = scope.ServiceProvider.GetRequiredService<IMediaDeletionEngine>();
            var reconciliationService =
                scope.ServiceProvider.GetRequiredService<IMediaReconciliationService>();
            var statusService = scope.ServiceProvider.GetService<IMediaCleanupStatusService>();
            var totalDeleted = 0;
            var totalTombstoned = 0;
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

                var purgeOperation = new MediaDeletionOperationContext(
                    MediaCleanupTypes.Purge, "scheduled", _instanceId);
                var purgeResult = await deletionEngine.ExecuteOperationAsync(
                    purgeOperation,
                    () => ProcessPurgeAsync(
                        context, deletionEngine, purgeOperation, stoppingToken),
                    stoppingToken);
                totalDeleted += purgeResult.FilesDeleted;
                totalBytesFreed += purgeResult.BytesFreed;
                operationFailures += purgeResult.Failures;

                if (_options.EnableExpirationCleanup)
                {
                    var operation = new MediaDeletionOperationContext(
                        MediaCleanupTypes.Expiration, "scheduled", _instanceId);
                    var result = await deletionEngine.ExecuteOperationAsync(
                        operation,
                        () => ProcessExpiredMediaAsync(
                            context, deletionEngine, operation,
                            processedRecordIds, stoppingToken),
                        stoppingToken);
                    totalDeleted += result.FilesDeleted;
                    totalTombstoned += result.RecordsTombstoned;
                    totalBytesFreed += result.BytesFreed;
                    operationFailures += result.Failures;
                }

                if (_options.EnableReconciliation)
                {
                    var operation = new MediaDeletionOperationContext(
                        MediaCleanupTypes.Reconciliation, "scheduled", _instanceId);
                    var result = await deletionEngine.ExecuteOperationAsync(
                        operation,
                        () => reconciliationService.ReconcileAsync(
                            operation, stoppingToken),
                        stoppingToken);
                    totalDeleted += result.FilesDeleted;
                    totalTombstoned += result.RecordsTombstoned;
                    totalBytesFreed += result.BytesFreed;
                    operationFailures += result.Failures;
                }

                if (_options.EnableRetentionCleanup)
                {
                    var operation = new MediaDeletionOperationContext(
                        MediaCleanupTypes.Retention, "scheduled", _instanceId);
                    var result = await deletionEngine.ExecuteOperationAsync(
                        operation,
                        () => ProcessRetentionMediaAsync(
                            context, deletionEngine, operation,
                            processedRecordIds, stoppingToken),
                        stoppingToken);
                    totalDeleted += result.FilesDeleted;
                    totalTombstoned += result.RecordsTombstoned;
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
                    "Media cleanup {Status}. Permanently deleted {DeletedCount} files, tombstoned {TombstonedCount} records, and freed {Bytes:N0} bytes in {Duration:F2}s",
                    status,
                    totalDeleted,
                    totalTombstoned,
                    totalBytesFreed,
                    stopwatch.Elapsed.TotalSeconds);

                // Record run completion for status tracking
                if (statusService != null)
                {
                    await statusService.RecordRunCompletionAsync(
                        totalDeleted,
                        totalBytesFreed,
                        stopwatch.Elapsed.TotalSeconds,
                        status,
                        _instanceId,
                        "scheduled",
                        stoppingToken);
                }

            }
        }

        private async Task<MediaDeletionEngineResult> ProcessPurgeAsync(
            IConfigurationDbContext context,
            IMediaDeletionEngine deletionEngine,
            MediaDeletionOperationContext operation,
            CancellationToken stoppingToken)
        {
            var defaultPolicyGrace = await context.MediaRetentionPolicies
                .Where(policy => policy.IsDefault && policy.IsActive)
                .Select(policy => (int?)policy.SoftDeleteGracePeriodDays)
                .FirstOrDefaultAsync(stoppingToken);
            var fallbackGrace = defaultPolicyGrace ?? _options.SoftDeleteGracePeriodDays;
            var tombstoneQuery = context.MediaRecords
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(media => media.DeletedAt != null);

            if (_options.TestVirtualKeyGroups.Any())
            {
                tombstoneQuery = tombstoneQuery.Where(media => context.VirtualKeys.Any(key =>
                    key.Id == media.VirtualKeyId &&
                    _options.TestVirtualKeyGroups.Contains(key.VirtualKeyGroupId)));
            }

            var tombstones = await tombstoneQuery
                .Select(media => new
                {
                    Media = media,
                    AssignedPolicyGrace = context.VirtualKeys
                        .Where(key =>
                            key.Id == media.VirtualKeyId &&
                            key.VirtualKeyGroup.MediaRetentionPolicyId != null)
                        .Select(key => (int?)key.VirtualKeyGroup.MediaRetentionPolicy!
                            .SoftDeleteGracePeriodDays)
                        .FirstOrDefault()
                })
                .ToListAsync(stoppingToken);
            var now = DateTime.UtcNow;
            var purgeCandidates = tombstones
                .Where(item => item.Media.DeletedAt <
                    now.AddDays(-Math.Max(0, item.AssignedPolicyGrace ?? fallbackGrace)))
                .Select(item => item.Media)
                .ToList();

            _logger.LogInformation(
                "Found {Count} soft-deleted media files whose recovery window has elapsed",
                purgeCandidates.Count);

            return await deletionEngine.DeleteAsync(
                new MediaDeletionRequest(
                    purgeCandidates,
                    operation,
                    Purge: true),
                stoppingToken);
        }

        private async Task<MediaDeletionEngineResult> ProcessExpiredMediaAsync(
            IConfigurationDbContext context,
            IMediaDeletionEngine deletionEngine,
            MediaDeletionOperationContext operation,
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

            return await deletionEngine.DeleteAsync(
                new MediaDeletionRequest(
                    expiredMedia,
                    operation,
                    ProcessedRecordIds: processedRecordIds),
                stoppingToken);
        }

        private async Task<MediaDeletionEngineResult> ProcessRetentionMediaAsync(
            IConfigurationDbContext context,
            IMediaDeletionEngine deletionEngine,
            MediaDeletionOperationContext operation,
            HashSet<Guid> processedRecordIds,
            CancellationToken stoppingToken)
        {
            IQueryable<int> groupQuery = context.VirtualKeyGroups.Select(group => group.Id);
            if (_options.TestVirtualKeyGroups.Any())
            {
                groupQuery = groupQuery.Where(groupId => _options.TestVirtualKeyGroups.Contains(groupId));
            }

            var groupIds = await groupQuery.ToListAsync(stoppingToken);
            var result = MediaDeletionEngineResult.Empty;

            foreach (var groupId in groupIds)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var groupResult = await ProcessGroupAsync(
                    groupId,
                    context,
                    deletionEngine,
                    operation,
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

        private async Task<MediaDeletionEngineResult> ProcessGroupAsync(
            int groupId,
            IConfigurationDbContext context,
            IMediaDeletionEngine deletionEngine,
            MediaDeletionOperationContext operation,
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
                    return MediaDeletionEngineResult.Empty;
                }

                var retention = await ResolveRetentionSettingsAsync(group, context, stoppingToken);
                if (retention == null)
                    return MediaDeletionEngineResult.Empty;

                var mediaToDelete = await QueryEligibleMediaAsync(
                    group, retention.Value, context, stoppingToken);
                if (mediaToDelete == null || mediaToDelete.Count == 0)
                    return MediaDeletionEngineResult.Empty;

                // Process deletions in batches
                return await deletionEngine.DeleteAsync(
                    new MediaDeletionRequest(
                        mediaToDelete,
                        operation,
                        groupId,
                        processedRecordIds),
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
                return new MediaDeletionEngineResult(Failures: 1);
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
