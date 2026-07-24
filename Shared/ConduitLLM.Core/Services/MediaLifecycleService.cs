using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service implementation for managing the lifecycle of media files.
    /// </summary>
    public class MediaLifecycleService : IMediaLifecycleService
    {
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IVirtualKeyRepository? _virtualKeyRepository;
        private readonly IMediaQuotaService? _mediaQuotaService;
        private readonly IConfigurationDbContext? _configurationDbContext;
        private readonly ILogger<MediaLifecycleService> _logger;

        /// <summary>
        /// Initializes a new instance of the MediaLifecycleService class.
        /// </summary>
        /// <param name="mediaRepository">The media record repository.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="virtualKeyRepository">The virtual key repository (optional, needed for group filtering).</param>
        public MediaLifecycleService(
            IMediaRecordRepository mediaRepository,
            ILogger<MediaLifecycleService> logger,
            IVirtualKeyRepository? virtualKeyRepository = null,
            IMediaQuotaService? mediaQuotaService = null,
            IConfigurationDbContext? configurationDbContext = null)
        {
            _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _virtualKeyRepository = virtualKeyRepository;
            _mediaQuotaService = mediaQuotaService;
            _configurationDbContext = configurationDbContext;
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

            await _mediaRepository.CreateAsync(mediaRecord);

            _logger.LogInformation(
                "Tracked media {StorageKey} of type {MediaType} for virtual key {VirtualKeyId}",
                storageKey, mediaType, virtualKeyId);

            return mediaRecord;
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
                if (_configurationDbContext != null)
                {
                    return await GetAggregatedOverallStorageStatsAsync(virtualKeyGroupId);
                }

                List<MediaRecord> allMedia;
                Dictionary<string, long> byProvider;
                
                if (virtualKeyGroupId.HasValue)
                {
                    if (_virtualKeyRepository == null)
                    {
                        throw new InvalidOperationException("Virtual key repository is not configured. Cannot filter by group.");
                    }
                    
                    // Get virtual keys for this group
                    var virtualKeys = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                        _virtualKeyRepository.GetByVirtualKeyGroupIdPaginatedAsync, virtualKeyGroupId.Value);
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
                }
                else
                {
                    byProvider = await _mediaRepository.GetStorageStatsByProviderAsync();
                    
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
                    // FK cascade makes database-side orphan rows impossible. Storage-side
                    // drift is reported by MediaCleanupStatusDto after reconciliation.
                    OrphanedFiles = 0,
                    ByProvider = byProvider,
                    ByMediaType = byMediaType,
                    StorageByVirtualKey = storageByVirtualKey,
                    GroupQuotaUsage = _mediaQuotaService == null
                        ? Array.Empty<MediaGroupQuotaUsage>()
                        : await _mediaQuotaService.GetGroupUsagesAsync(virtualKeyGroupId)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overall storage stats");
                throw;
            }
        }

        private async Task<OverallMediaStorageStats> GetAggregatedOverallStorageStatsAsync(
            int? virtualKeyGroupId)
        {
            var mediaQuery = _configurationDbContext!.MediaRecords.AsNoTracking();
            if (virtualKeyGroupId.HasValue)
            {
                mediaQuery = mediaQuery.Where(media =>
                    _configurationDbContext.VirtualKeys.Any(key =>
                        key.Id == media.VirtualKeyId &&
                        key.VirtualKeyGroupId == virtualKeyGroupId.Value));
            }

            var totals = await mediaQuery
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    TotalFiles = group.Count(),
                    TotalSizeBytes = group.Sum(media => media.SizeBytes ?? 0)
                })
                .SingleOrDefaultAsync();
            var providerRows = await mediaQuery
                .GroupBy(media => media.Provider ?? "unknown")
                .Select(group => new
                {
                    Provider = group.Key,
                    SizeBytes = group.Sum(media => media.SizeBytes ?? 0)
                })
                .ToListAsync();
            var typeRows = await mediaQuery
                .GroupBy(media => media.MediaType)
                .Select(group => new
                {
                    MediaType = group.Key,
                    FileCount = group.Count(),
                    SizeBytes = group.Sum(media => media.SizeBytes ?? 0)
                })
                .ToListAsync();
            var virtualKeyRows = await mediaQuery
                .GroupBy(media => media.VirtualKeyId)
                .Select(group => new
                {
                    VirtualKeyId = group.Key,
                    SizeBytes = group.Sum(media => media.SizeBytes ?? 0)
                })
                .ToListAsync();

            return new OverallMediaStorageStats
            {
                TotalFiles = totals?.TotalFiles ?? 0,
                TotalSizeBytes = totals?.TotalSizeBytes ?? 0,
                OrphanedFiles = 0,
                ByProvider = providerRows.ToDictionary(row => row.Provider, row => row.SizeBytes),
                ByMediaType = typeRows.ToDictionary(
                    row => row.MediaType,
                    row => new MediaTypeStats
                    {
                        FileCount = row.FileCount,
                        SizeBytes = row.SizeBytes
                    }),
                StorageByVirtualKey = virtualKeyRows.ToDictionary(
                    row => row.VirtualKeyId.ToString(),
                    row => row.SizeBytes),
                GroupQuotaUsage = _mediaQuotaService == null
                    ? Array.Empty<MediaGroupQuotaUsage>()
                    : await _mediaQuotaService.GetGroupUsagesAsync(virtualKeyGroupId)
            };
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

}
