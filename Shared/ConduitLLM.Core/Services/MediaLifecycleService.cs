using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service implementation for managing the lifecycle of media files.
    /// </summary>
    public class MediaLifecycleService : IMediaLifecycleService
    {
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IMediaQuotaService? _mediaQuotaService;
        private readonly ILogger<MediaLifecycleService> _logger;
        private const int VirtualKeyStatsLimit = 100;

        /// <summary>
        /// Initializes a new instance of the MediaLifecycleService class.
        /// </summary>
        /// <param name="mediaRepository">The media record repository.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="mediaQuotaService">Optional group quota reporting service.</param>
        public MediaLifecycleService(
            IMediaRecordRepository mediaRepository,
            ILogger<MediaLifecycleService> logger,
            IMediaQuotaService? mediaQuotaService = null)
        {
            _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediaQuotaService = mediaQuotaService;
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
                var aggregate = await _mediaRepository.GetAggregateStorageStatsAsync(
                    virtualKeyGroupId,
                    VirtualKeyStatsLimit);
                return new OverallMediaStorageStats
                {
                    TotalSizeBytes = aggregate.TotalSizeBytes,
                    TotalFiles = aggregate.TotalFiles,
                    OrphanedFiles = 0,
                    ByProvider = aggregate.ByProvider.ToDictionary(),
                    ByMediaType = aggregate.ByMediaType.ToDictionary(
                        row => row.MediaType,
                        row => new MediaTypeStats
                        {
                            FileCount = row.FileCount,
                            SizeBytes = row.SizeBytes
                        }),
                    StorageByVirtualKey = aggregate.TopVirtualKeys.ToDictionary(
                        row => row.VirtualKeyId.ToString(),
                        row => row.SizeBytes),
                    GroupQuotaUsage = _mediaQuotaService == null
                        ? Array.Empty<MediaGroupQuotaUsage>()
                        : await _mediaQuotaService.GetGroupUsagesAsync(virtualKeyGroupId)
                };
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

}
