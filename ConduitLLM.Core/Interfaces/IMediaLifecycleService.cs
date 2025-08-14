using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service interface for managing the lifecycle of media files including tracking, cleanup, and maintenance.
    /// </summary>
    public interface IMediaLifecycleService
    {
        /// <summary>
        /// Tracks a newly generated media file by creating a media record.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key that owns the media.</param>
        /// <param name="storageKey">The storage key identifying the media file.</param>
        /// <param name="mediaType">The type of media (image or video).</param>
        /// <param name="metadata">Additional metadata about the media file.</param>
        /// <returns>The created media record.</returns>
        Task<MediaRecord> TrackMediaAsync(
            int virtualKeyId, 
            string storageKey, 
            string mediaType, 
            MediaLifecycleMetadata metadata);

        /// <summary>
        /// Deletes all media associated with a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Number of media files deleted.</returns>
        Task<int> DeleteMediaForVirtualKeyAsync(int virtualKeyId);

        /// <summary>
        /// Cleans up expired media files.
        /// </summary>
        /// <returns>Number of media files cleaned up.</returns>
        Task<int> CleanupExpiredMediaAsync();

        /// <summary>
        /// Cleans up orphaned media files (where virtual key no longer exists).
        /// </summary>
        /// <returns>Number of orphaned media files cleaned up.</returns>
        Task<int> CleanupOrphanedMediaAsync();

        /// <summary>
        /// Prunes old media files based on retention policy.
        /// </summary>
        /// <param name="daysToKeep">Number of days to keep media files.</param>
        /// <param name="respectRecentAccess">If true, skip files accessed recently.</param>
        /// <returns>Number of media files pruned.</returns>
        Task<int> PruneOldMediaAsync(int daysToKeep, bool respectRecentAccess = true);

        /// <summary>
        /// Updates access statistics for a media file.
        /// </summary>
        /// <param name="storageKey">The storage key of the media file.</param>
        /// <returns>True if stats were updated, false if media not found.</returns>
        Task<bool> UpdateAccessStatsAsync(string storageKey);

        /// <summary>
        /// Gets storage statistics for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Storage statistics for the virtual key.</returns>
        Task<MediaStorageStats> GetStorageStatsByVirtualKeyAsync(int virtualKeyId);

        /// <summary>
        /// Gets overall storage statistics.
        /// </summary>
        /// <returns>Overall storage statistics.</returns>
        Task<OverallMediaStorageStats> GetOverallStorageStatsAsync();

        /// <summary>
        /// Gets media records for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>List of media records.</returns>
        Task<List<MediaRecord>> GetMediaByVirtualKeyAsync(int virtualKeyId);
    }

    /// <summary>
    /// Metadata about a media file for lifecycle tracking.
    /// </summary>
    public class MediaLifecycleMetadata
    {
        /// <summary>
        /// Gets or sets the content type of the media.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets the size of the media file in bytes.
        /// </summary>
        public long? SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the hash of the media content.
        /// </summary>
        public string? ContentHash { get; set; }

        /// <summary>
        /// Gets or sets the provider used to generate the media.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the model used to generate the media.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the prompt used to generate the media.
        /// </summary>
        public string? Prompt { get; set; }

        /// <summary>
        /// Gets or sets the storage URL.
        /// </summary>
        public string? StorageUrl { get; set; }

        /// <summary>
        /// Gets or sets the public CDN URL.
        /// </summary>
        public string? PublicUrl { get; set; }

        /// <summary>
        /// Gets or sets when the media expires.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Storage statistics for a virtual key.
    /// </summary>
    public class MediaStorageStats
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Gets or sets the total storage size in bytes.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the total number of media files.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the breakdown by media type.
        /// </summary>
        public Dictionary<string, MediaTypeStats> ByMediaType { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a specific media type.
    /// </summary>
    public class MediaTypeStats
    {
        /// <summary>
        /// Gets or sets the number of files.
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Gets or sets the total size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Overall storage statistics.
    /// </summary>
    public class OverallMediaStorageStats
    {
        /// <summary>
        /// Gets or sets the total storage size across all virtual keys.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the total number of media files.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned files.
        /// </summary>
        public int OrphanedFiles { get; set; }

        /// <summary>
        /// Gets or sets the breakdown by provider.
        /// </summary>
        public Dictionary<string, long> ByProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets the breakdown by media type.
        /// </summary>
        public Dictionary<string, MediaTypeStats> ByMediaType { get; set; } = new();
    }
}