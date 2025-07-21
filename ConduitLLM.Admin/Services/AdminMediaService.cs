using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for administrative media management operations.
    /// </summary>
    public class AdminMediaService : IAdminMediaService
    {
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IMediaLifecycleService _mediaLifecycleService;
        private readonly ILogger<AdminMediaService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminMediaService class.
        /// </summary>
        /// <param name="mediaRepository">The media record repository.</param>
        /// <param name="mediaLifecycleService">The media lifecycle service.</param>
        /// <param name="logger">The logger instance.</param>
        public AdminMediaService(
            IMediaRecordRepository mediaRepository,
            IMediaLifecycleService mediaLifecycleService,
            ILogger<AdminMediaService> logger)
        {
            _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
            _mediaLifecycleService = mediaLifecycleService ?? throw new ArgumentNullException(nameof(mediaLifecycleService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<OverallMediaStorageStats> GetOverallStorageStatsAsync()
        {
            try
            {
                _logger.LogInformation("Getting overall storage statistics");
                return await _mediaLifecycleService.GetOverallStorageStatsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overall storage statistics");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaStorageStats> GetStorageStatsByVirtualKeyAsync(int virtualKeyId)
        {
            try
            {
                _logger.LogInformation("Getting storage statistics for virtual key {VirtualKeyId}", virtualKeyId);
                return await _mediaLifecycleService.GetStorageStatsByVirtualKeyAsync(virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetMediaByVirtualKeyAsync(int virtualKeyId)
        {
            try
            {
                _logger.LogInformation("Getting media records for virtual key {VirtualKeyId}", virtualKeyId);
                return await _mediaLifecycleService.GetMediaByVirtualKeyAsync(virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media records for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CleanupExpiredMediaAsync()
        {
            try
            {
                _logger.LogInformation("Manually triggering expired media cleanup");
                var count = await _mediaLifecycleService.CleanupExpiredMediaAsync();
                _logger.LogInformation("Cleaned up {Count} expired media files", count);
                return count;
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
            try
            {
                _logger.LogInformation("Manually triggering orphaned media cleanup");
                var count = await _mediaLifecycleService.CleanupOrphanedMediaAsync();
                _logger.LogInformation("Cleaned up {Count} orphaned media files", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned media cleanup");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> PruneOldMediaAsync(int daysToKeep)
        {
            try
            {
                if (daysToKeep <= 0)
                {
                    throw new ArgumentException("Days to keep must be positive", nameof(daysToKeep));
                }

                _logger.LogInformation("Manually triggering old media pruning (keeping last {Days} days)", daysToKeep);
                var count = await _mediaLifecycleService.PruneOldMediaAsync(daysToKeep, respectRecentAccess: true);
                _logger.LogInformation("Pruned {Count} old media files", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during old media pruning");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteMediaAsync(Guid mediaId)
        {
            try
            {
                _logger.LogInformation("Deleting media record {MediaId}", mediaId);
                
                var mediaRecord = await _mediaRepository.GetByIdAsync(mediaId);
                if (mediaRecord == null)
                {
                    _logger.LogWarning("Media record {MediaId} not found", mediaId);
                    return false;
                }

                // Delete from storage first
                try
                {
                    var storageService = _mediaLifecycleService as IMediaStorageService;
                    if (storageService != null)
                    {
                        await storageService.DeleteAsync(mediaRecord.StorageKey);
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogError(storageEx, "Failed to delete media {StorageKey} from storage, continuing with database deletion", mediaRecord.StorageKey);
                }

                // Delete from database
                var result = await _mediaRepository.DeleteAsync(mediaId);
                
                if (result)
                {
                    _logger.LogInformation("Successfully deleted media record {MediaId}", mediaId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting media record {MediaId}", mediaId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> SearchMediaByStorageKeyAsync(string storageKeyPattern)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(storageKeyPattern))
                {
                    return new List<MediaRecord>();
                }

                _logger.LogInformation("Searching media by storage key pattern: {Pattern}", storageKeyPattern);
                
                // Get all media records and filter by pattern
                // Note: This is not efficient for large datasets. In production, consider adding a repository method for pattern matching
                var allMedia = await _mediaRepository.GetMediaOlderThanAsync(DateTime.UtcNow.AddYears(10)); // Get all
                
                var matchingMedia = allMedia
                    .Where(m => m.StorageKey.Contains(storageKeyPattern, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();
                
                _logger.LogInformation("Found {Count} media records matching pattern", matchingMedia.Count);
                return matchingMedia;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching media by storage key pattern");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>> GetStorageStatsByProviderAsync()
        {
            try
            {
                _logger.LogInformation("Getting storage statistics by provider");
                return await _mediaRepository.GetStorageStatsByProviderAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics by provider");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync()
        {
            try
            {
                _logger.LogInformation("Getting storage statistics by media type");
                return await _mediaRepository.GetStorageStatsByMediaTypeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics by media type");
                throw;
            }
        }
    }
}