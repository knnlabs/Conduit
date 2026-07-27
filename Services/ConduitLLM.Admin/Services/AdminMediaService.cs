using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for administrative media management operations.
    /// </summary>
    public class AdminMediaService : IAdminMediaService
    {
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IMediaLifecycleService _mediaLifecycleService;
        private readonly IConfigurationDbContext _configurationContext;
        private readonly IDistributedLockService _cleanupLockService;
        private readonly IMediaDeletionEngine _deletionEngine;
        private readonly MediaLifecycleOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AdminMediaService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminMediaService class.
        /// </summary>
        /// <param name="mediaRepository">The media record repository.</param>
        /// <param name="mediaLifecycleService">The media lifecycle service.</param>
        /// <param name="configurationContext">Configuration data used to resolve retention policies.</param>
        /// <param name="cleanupLockService">Shared lock that serializes restore with permanent purge.</param>
        /// <param name="deletionEngine">Shared guarded engine for permanent storage deletion.</param>
        /// <param name="options">Media lifecycle options.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="timeProvider">Clock used to enforce the recovery window.</param>
        public AdminMediaService(
            IMediaRecordRepository mediaRepository,
            IMediaLifecycleService mediaLifecycleService,
            IConfigurationDbContext configurationContext,
            IDistributedLockService cleanupLockService,
            IMediaDeletionEngine deletionEngine,
            IOptions<MediaLifecycleOptions> options,
            ILogger<AdminMediaService> logger,
            TimeProvider? timeProvider = null)
        {
            _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
            _mediaLifecycleService = mediaLifecycleService ?? throw new ArgumentNullException(nameof(mediaLifecycleService));
            _configurationContext = configurationContext ??
                throw new ArgumentNullException(nameof(configurationContext));
            _cleanupLockService = cleanupLockService ??
                throw new ArgumentNullException(nameof(cleanupLockService));
            _deletionEngine = deletionEngine ??
                throw new ArgumentNullException(nameof(deletionEngine));
            _options = options.Value;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<OverallMediaStorageStats> GetOverallStorageStatsAsync(int? virtualKeyGroupId = null)
        {
            if (virtualKeyGroupId.HasValue)
            {
                _logger.LogDebug("Getting storage statistics for virtual key group {GroupId}", virtualKeyGroupId.Value);
            }
            else
            {
                _logger.LogDebug("Getting overall storage statistics");
            }
            return await _mediaLifecycleService.GetOverallStorageStatsAsync(virtualKeyGroupId);
        }

        /// <inheritdoc/>
        public async Task<MediaStorageStats> GetStorageStatsByVirtualKeyAsync(int virtualKeyId)
        {
            _logger.LogDebug("Getting storage statistics for virtual key {VirtualKeyId}", virtualKeyId);
            return await _mediaLifecycleService.GetStorageStatsByVirtualKeyAsync(virtualKeyId);
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetMediaByVirtualKeyAsync(
            int virtualKeyId,
            bool includeDeleted = false)
        {
            _logger.LogDebug("Getting media records for virtual key {VirtualKeyId}", virtualKeyId);
            return includeDeleted
                ? await _mediaRepository.GetByVirtualKeyIdAsync(
                    virtualKeyId,
                    includeDeleted: true)
                : await _mediaLifecycleService.GetMediaByVirtualKeyAsync(virtualKeyId);
        }

        /// <inheritdoc/>
        public async Task<AdminMediaDeleteResult?> DeleteMediaAsync(Guid mediaId)
        {
            try
            {
                _logger.LogDebug("Deleting media record {MediaId}", mediaId);

                var mediaRecord = await _mediaRepository.GetByIdAsync(mediaId);
                if (mediaRecord == null)
                {
                    _logger.LogWarning("Media record {MediaId} not found", mediaId);
                    return null;
                }

                if (_options.EnableSoftDelete)
                {
                    var deletedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    if (!await _mediaRepository.TombstoneAsync(mediaId, deletedAt))
                    {
                        return null;
                    }

                    _logger.LogInformation(
                        "Soft-deleted media record {MediaId}; storage {StorageKey} remains until purge",
                        mediaId,
                        mediaRecord.StorageKey);
                    return new AdminMediaDeleteResult(true, deletedAt);
                }

                await using var lockHandle = await _cleanupLockService.AcquireLockAsync(
                    MediaCleanupLock.Key,
                    MediaCleanupLock.Duration);
                if (lockHandle == null)
                {
                    throw new InvalidOperationException(
                        "Media deletion is blocked while another cleanup operation is active.");
                }

                var operation = new MediaDeletionOperationContext(
                    MediaCleanupTypes.Manual,
                    "manual",
                    $"manual:{mediaId}",
                    Force: true);
                var result = await _deletionEngine.ExecuteOperationAsync(
                    operation,
                    () => _deletionEngine.DeleteAsync(
                        new MediaDeletionRequest(
                            new[] { mediaRecord },
                            operation,
                            Purge: true)));
                if (result.BudgetExhausted || result.Failures > 0 || result.FilesDeleted != 1)
                {
                    throw new InvalidOperationException(
                        $"Media deletion was not completed " +
                        $"(budgetExhausted={result.BudgetExhausted}, failures={result.Failures}).");
                }

                _logger.LogInformation(
                    "Successfully deleted media record {MediaId} and storage {StorageKey}",
                    mediaId,
                    mediaRecord.StorageKey);
                return new AdminMediaDeleteResult(false, null);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaRestoreOutcome> RestoreMediaAsync(Guid mediaId)
        {
            await using var lockHandle = await _cleanupLockService.AcquireLockAsync(
                MediaCleanupLock.Key,
                MediaCleanupLock.Duration);
            if (lockHandle == null)
            {
                return MediaRestoreOutcome.CleanupInProgress;
            }

            var mediaRecord = await _mediaRepository.GetByIdIncludingDeletedAsync(mediaId);
            if (mediaRecord == null)
            {
                return MediaRestoreOutcome.NotFound;
            }

            if (!mediaRecord.DeletedAt.HasValue)
            {
                return MediaRestoreOutcome.NotDeleted;
            }

            var gracePeriodDays = await ResolveGracePeriodDaysAsync(
                mediaRecord.VirtualKeyId);
            var restoreDeadline = mediaRecord.DeletedAt.Value
                .AddDays(gracePeriodDays);
            if (_timeProvider.GetUtcNow().UtcDateTime >= restoreDeadline)
            {
                return MediaRestoreOutcome.GracePeriodElapsed;
            }

            return await _mediaRepository.RestoreAsync(mediaId)
                ? MediaRestoreOutcome.Restored
                : MediaRestoreOutcome.NotFound;
        }

        private async Task<int> ResolveGracePeriodDaysAsync(int virtualKeyId)
        {
            var assignedGrace = await _configurationContext.VirtualKeys
                .Where(key =>
                    key.Id == virtualKeyId &&
                    key.VirtualKeyGroup.MediaRetentionPolicyId != null)
                .Select(key => (int?)key.VirtualKeyGroup.MediaRetentionPolicy!
                    .SoftDeleteGracePeriodDays)
                .FirstOrDefaultAsync();
            if (assignedGrace.HasValue)
            {
                return Math.Max(0, assignedGrace.Value);
            }

            var defaultGrace = await _configurationContext.MediaRetentionPolicies
                .Where(policy => policy.IsDefault && policy.IsActive)
                .Select(policy => (int?)policy.SoftDeleteGracePeriodDays)
                .FirstOrDefaultAsync();
            return Math.Max(
                0,
                defaultGrace ?? _options.SoftDeleteGracePeriodDays);
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> SearchMediaByStorageKeyAsync(string storageKeyPattern)
        {
            if (string.IsNullOrWhiteSpace(storageKeyPattern))
            {
                return new List<MediaRecord>();
            }

            _logger.LogInformation("Searching media by storage key pattern: {Pattern}", storageKeyPattern);

            // Use database-level filtering for efficient pattern matching
            var matchingMedia = await _mediaRepository.SearchByStorageKeyPatternAsync(storageKeyPattern);

            _logger.LogInformation("Found {Count} media records matching pattern", matchingMedia.Count);
            return matchingMedia;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>> GetStorageStatsByProviderAsync()
        {
            _logger.LogDebug("Getting storage statistics by provider");
            return await _mediaRepository.GetStorageStatsByProviderAsync();
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync()
        {
            _logger.LogDebug("Getting storage statistics by media type");
            return await _mediaRepository.GetStorageStatsByMediaTypeAsync();
        }
    }
}
