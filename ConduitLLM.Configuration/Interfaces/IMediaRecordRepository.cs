using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for media record operations.
    /// </summary>
    public interface IMediaRecordRepository
    {
        /// <summary>
        /// Creates a new media record.
        /// </summary>
        /// <param name="mediaRecord">The media record to create.</param>
        /// <returns>The created media record.</returns>
        Task<MediaRecord> CreateAsync(MediaRecord mediaRecord);

        /// <summary>
        /// Gets a media record by its ID.
        /// </summary>
        /// <param name="id">The ID of the media record.</param>
        /// <returns>The media record if found, null otherwise.</returns>
        Task<MediaRecord?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets a media record by its storage key.
        /// </summary>
        /// <param name="storageKey">The storage key of the media record.</param>
        /// <returns>The media record if found, null otherwise.</returns>
        Task<MediaRecord?> GetByStorageKeyAsync(string storageKey);

        /// <summary>
        /// Gets all media records for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>List of media records for the virtual key.</returns>
        Task<List<MediaRecord>> GetByVirtualKeyIdAsync(int virtualKeyId);

        /// <summary>
        /// Gets media records that have expired.
        /// </summary>
        /// <param name="currentTime">The current time to compare against.</param>
        /// <returns>List of expired media records.</returns>
        Task<List<MediaRecord>> GetExpiredMediaAsync(DateTime currentTime);

        /// <summary>
        /// Gets media records older than a specified date.
        /// </summary>
        /// <param name="cutoffDate">The cutoff date.</param>
        /// <returns>List of old media records.</returns>
        Task<List<MediaRecord>> GetMediaOlderThanAsync(DateTime cutoffDate);

        /// <summary>
        /// Gets orphaned media records (where virtual key no longer exists).
        /// </summary>
        /// <returns>List of orphaned media records.</returns>
        Task<List<MediaRecord>> GetOrphanedMediaAsync();

        /// <summary>
        /// Updates access statistics for a media record.
        /// </summary>
        /// <param name="id">The ID of the media record.</param>
        /// <returns>True if updated successfully, false otherwise.</returns>
        Task<bool> UpdateAccessStatsAsync(Guid id);

        /// <summary>
        /// Deletes a media record.
        /// </summary>
        /// <param name="id">The ID of the media record to delete.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Deletes multiple media records.
        /// </summary>
        /// <param name="ids">The IDs of the media records to delete.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> DeleteManyAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Gets the total storage size used by a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Total storage size in bytes.</returns>
        Task<long> GetTotalStorageSizeByVirtualKeyAsync(int virtualKeyId);

        /// <summary>
        /// Gets storage statistics grouped by provider.
        /// </summary>
        /// <returns>Dictionary of provider names to total storage size.</returns>
        Task<Dictionary<string, long>> GetStorageStatsByProviderAsync();

        /// <summary>
        /// Gets storage statistics grouped by media type.
        /// </summary>
        /// <returns>Dictionary of media types to total storage size.</returns>
        Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync();

        /// <summary>
        /// Gets the count of media records for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Count of media records.</returns>
        Task<int> GetCountByVirtualKeyAsync(int virtualKeyId);
    }
}