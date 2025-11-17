using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service implementation for managing the lifecycle of media files.
    /// </summary>
    public class MediaLifecycleService : IMediaLifecycleService
    {
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IMediaStorageService _storageService;
        private readonly IVirtualKeyRepository? _virtualKeyRepository;
        private readonly ILogger<MediaLifecycleService> _logger;
        private readonly MediaManagementOptions _options;

        /// <summary>
        /// Initializes a new instance of the MediaLifecycleService class.
        /// </summary>
        /// <param name="mediaRepository">The media record repository.</param>
        /// <param name="storageService">The media storage service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">Media management configuration options.</param>
        /// <param name="virtualKeyRepository">The virtual key repository (optional, needed for group filtering).</param>
        public MediaLifecycleService(
            IMediaRecordRepository mediaRepository,
            IMediaStorageService storageService,
            ILogger<MediaLifecycleService> logger,
            IOptions<MediaManagementOptions> options,
            IVirtualKeyRepository? virtualKeyRepository = null)
        {
            _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new MediaManagementOptions();
            _virtualKeyRepository = virtualKeyRepository;
        }

        /// <inheritdoc/>
        public async Task<MediaRecord> TrackMediaAsync(
            int virtualKeyId, 
            string storageKey, 
            string mediaType, 
            MediaLifecycleMetadata metadata)
        {
            if (virtualKeyId <= 0)
                throw new ArgumentException("Virtual key ID must be positive", nameof(virtualKeyId));
            
            if (string.IsNullOrWhiteSpace(storageKey))
                throw new ArgumentException("Storage key cannot be empty", nameof(storageKey));
            
            if (string.IsNullOrWhiteSpace(mediaType))
                throw new ArgumentException("Media type cannot be empty", nameof(mediaType));

            var mediaRecord = new MediaRecord
            {
                Id = Guid.NewGuid(),
                StorageKey = storageKey,
                VirtualKeyId = virtualKeyId,
                MediaType = mediaType,
                ContentType = metadata?.ContentType,
                SizeBytes = metadata?.SizeBytes,
                ContentHash = metadata?.ContentHash,
                Provider = metadata?.Provider,
                Model = metadata?.Model,
                Prompt = metadata?.Prompt,
                StorageUrl = metadata?.StorageUrl,
                PublicUrl = metadata?.PublicUrl,
                ExpiresAt = metadata?.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
                AccessCount = 0
            };

            var created = await _mediaRepository.CreateAsync(mediaRecord);
            
            _logger.LogInformation(
                "Tracked media {StorageKey} of type {MediaType} for virtual key {VirtualKeyId}",
                storageKey, mediaType, virtualKeyId);
            
            return created;
        }

        /// <inheritdoc/>
        public async Task<int> DeleteMediaForVirtualKeyAsync(int virtualKeyId)
        {
            if (!_options.EnableAutoCleanup)
            {
                _logger.LogWarning("Auto cleanup is disabled, skipping media deletion for virtual key {VirtualKeyId}", virtualKeyId);
                return 0;
            }

            try
            {
                var mediaRecords = await _mediaRepository.GetByVirtualKeyIdAsync(virtualKeyId);
                var deletedCount = 0;

                foreach (var media in mediaRecords)
                {
                    try
                    {
                        // Delete from storage
                        await _storageService.DeleteAsync(media.StorageKey);
                        
                        // Delete from database
                        await _mediaRepository.DeleteAsync(media.Id);
                        
                        deletedCount++;
                        
                        _logger.LogInformation(
                            "Deleted media {StorageKey} for virtual key {VirtualKeyId}",
                            media.StorageKey, virtualKeyId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to delete media {StorageKey} for virtual key {VirtualKeyId}",
                            media.StorageKey, virtualKeyId);
                    }
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting media for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CleanupExpiredMediaAsync()
        {
            if (!_options.EnableAutoCleanup)
            {
                _logger.LogDebug("Auto cleanup is disabled, skipping expired media cleanup");
                return 0;
            }

            try
            {
                var expiredMedia = await _mediaRepository.GetExpiredMediaAsync(DateTime.UtcNow);
                var deletedCount = 0;

                foreach (var media in expiredMedia)
                {
                    try
                    {
                        await _storageService.DeleteAsync(media.StorageKey);
                        await _mediaRepository.DeleteAsync(media.Id);
                        deletedCount++;
                        
                        _logger.LogInformation(
                            "Cleaned up expired media {StorageKey} that expired at {ExpiresAt}",
                            media.StorageKey, media.ExpiresAt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to cleanup expired media {StorageKey}", media.StorageKey);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired media files", deletedCount);
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during expired media cleanup");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CleanupOrphanedMediaAsync()
        {
            if (!_options.OrphanCleanupEnabled)
            {
                _logger.LogDebug("Orphan cleanup is disabled");
                return 0;
            }

            try
            {
                var orphanedMedia = await _mediaRepository.GetOrphanedMediaAsync();
                var deletedCount = 0;

                foreach (var media in orphanedMedia)
                {
                    try
                    {
                        await _storageService.DeleteAsync(media.StorageKey);
                        await _mediaRepository.DeleteAsync(media.Id);
                        deletedCount++;
                        
                        _logger.LogInformation(
                            "Cleaned up orphaned media {StorageKey} for non-existent virtual key {VirtualKeyId}",
                            media.StorageKey, media.VirtualKeyId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to cleanup orphaned media {StorageKey}", media.StorageKey);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} orphaned media files", deletedCount);
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned media cleanup");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> PruneOldMediaAsync(int daysToKeep, bool respectRecentAccess = true)
        {
            if (!_options.EnableAutoCleanup || daysToKeep <= 0)
            {
                _logger.LogDebug("Media pruning is disabled or invalid days to keep: {DaysToKeep}", daysToKeep);
                return 0;
            }

            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                var oldMedia = await _mediaRepository.GetMediaOlderThanAsync(cutoffDate);
                var deletedCount = 0;
                var recentAccessCutoff = DateTime.UtcNow.AddDays(-30);

                foreach (var media in oldMedia)
                {
                    try
                    {
                        // Skip if accessed recently and respectRecentAccess is true
                        if (respectRecentAccess && 
                            media.LastAccessedAt.HasValue && 
                            media.LastAccessedAt.Value > recentAccessCutoff)
                        {
                            _logger.LogDebug(
                                "Skipping media {StorageKey} due to recent access at {LastAccessedAt}",
                                media.StorageKey, media.LastAccessedAt);
                            continue;
                        }

                        await _storageService.DeleteAsync(media.StorageKey);
                        await _mediaRepository.DeleteAsync(media.Id);
                        deletedCount++;
                        
                        _logger.LogInformation(
                            "Pruned old media {StorageKey} created at {CreatedAt}",
                            media.StorageKey, media.CreatedAt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to prune old media {StorageKey}", media.StorageKey);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Pruned {Count} old media files", deletedCount);
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during old media pruning");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAccessStatsAsync(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
                return false;

            try
            {
                var mediaRecord = await _mediaRepository.GetByStorageKeyAsync(storageKey);
                if (mediaRecord == null)
                    return false;

                return await _mediaRepository.UpdateAccessStatsAsync(mediaRecord.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating access stats for media {StorageKey}", storageKey);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaStorageStats> GetStorageStatsByVirtualKeyAsync(int virtualKeyId)
        {
            try
            {
                var mediaRecords = await _mediaRepository.GetByVirtualKeyIdAsync(virtualKeyId);
                
                var stats = new MediaStorageStats
                {
                    VirtualKeyId = virtualKeyId,
                    TotalFiles = mediaRecords.Count,
                    TotalSizeBytes = mediaRecords.Sum(m => m.SizeBytes ?? 0)
                };

                // Group by media type
                var typeGroups = mediaRecords.GroupBy(m => m.MediaType);
                foreach (var group in typeGroups)
                {
                    stats.ByMediaType[group.Key] = new MediaTypeStats
                    {
                        FileCount = group.Count(),
                        SizeBytes = group.Sum(m => m.SizeBytes ?? 0)
                    };
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage stats for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<OverallMediaStorageStats> GetOverallStorageStatsAsync(int? virtualKeyGroupId = null)
        {
            try
            {
                List<MediaRecord> allMedia;
                Dictionary<string, long> byProvider;
                List<MediaRecord> orphanedMedia;
                
                if (virtualKeyGroupId.HasValue)
                {
                    if (_virtualKeyRepository == null)
                    {
                        throw new InvalidOperationException("Virtual key repository is not configured. Cannot filter by group.");
                    }
                    
                    // Get virtual keys for this group
                    var virtualKeys = await _virtualKeyRepository.GetByVirtualKeyGroupIdAsync(virtualKeyGroupId.Value);
                    var virtualKeyIds = virtualKeys.Select(vk => vk.Id).ToList();
                    
                    // Get media only for these virtual keys
                    allMedia = new List<MediaRecord>();
                    foreach (var keyId in virtualKeyIds)
                    {
                        var keyMedia = await _mediaRepository.GetByVirtualKeyIdAsync(keyId);
                        allMedia.AddRange(keyMedia);
                    }
                    
                    byProvider = allMedia.GroupBy(m => m.Provider ?? "unknown")
                        .ToDictionary(g => g.Key, g => g.Sum(m => m.SizeBytes ?? 0));
                    orphanedMedia = new List<MediaRecord>(); // No orphaned media when filtering by group
                }
                else
                {
                    byProvider = await _mediaRepository.GetStorageStatsByProviderAsync();
                    orphanedMedia = await _mediaRepository.GetOrphanedMediaAsync();
                    
                    // Get all media records to calculate proper stats by type
                    allMedia = await _mediaRepository.GetMediaOlderThanAsync(DateTime.UtcNow.AddYears(10));
                }
                
                // Group by media type to get both file count and size
                var byMediaType = new Dictionary<string, MediaTypeStats>();
                var mediaTypeGroups = allMedia.GroupBy(m => m.MediaType);
                
                foreach (var group in mediaTypeGroups)
                {
                    byMediaType[group.Key] = new MediaTypeStats
                    {
                        FileCount = group.Count(),
                        SizeBytes = group.Sum(m => m.SizeBytes ?? 0)
                    };
                }

                // Group by virtual key to get storage per key
                var storageByVirtualKey = new Dictionary<string, long>();
                var virtualKeyGroups = allMedia.GroupBy(m => m.VirtualKeyId);
                
                foreach (var group in virtualKeyGroups)
                {
                    storageByVirtualKey[group.Key.ToString()] = group.Sum(m => m.SizeBytes ?? 0);
                }

                var stats = new OverallMediaStorageStats
                {
                    TotalSizeBytes = allMedia.Sum(m => m.SizeBytes ?? 0),
                    TotalFiles = allMedia.Count,
                    OrphanedFiles = orphanedMedia.Count,
                    ByProvider = byProvider,
                    ByMediaType = byMediaType,
                    StorageByVirtualKey = storageByVirtualKey
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overall storage stats");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetMediaByVirtualKeyAsync(int virtualKeyId)
        {
            try
            {
                return await _mediaRepository.GetByVirtualKeyIdAsync(virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }
    }

    /// <summary>
    /// Configuration options for media management.
    /// </summary>
    public class MediaManagementOptions
    {
        /// <summary>
        /// Gets or sets whether ownership tracking is enabled.
        /// </summary>
        public bool EnableOwnershipTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets whether automatic cleanup is enabled.
        /// </summary>
        public bool EnableAutoCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets the default media retention period in days.
        /// </summary>
        public int MediaRetentionDays { get; set; } = 90;

        /// <summary>
        /// Gets or sets whether orphan cleanup is enabled.
        /// </summary>
        public bool OrphanCleanupEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether access control is enabled.
        /// </summary>
        public bool AccessControlEnabled { get; set; } = false;
    }
}