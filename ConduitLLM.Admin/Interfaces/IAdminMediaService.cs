using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for administrative media management operations.
    /// </summary>
    public interface IAdminMediaService
    {
        /// <summary>
        /// Gets storage statistics for all virtual keys.
        /// </summary>
        /// <param name="virtualKeyGroupId">Optional filter by virtual key group ID</param>
        /// <returns>Overall storage statistics.</returns>
        Task<OverallMediaStorageStats> GetOverallStorageStatsAsync(int? virtualKeyGroupId = null);

        /// <summary>
        /// Gets storage statistics for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Storage statistics for the virtual key.</returns>
        Task<MediaStorageStats> GetStorageStatsByVirtualKeyAsync(int virtualKeyId);

        /// <summary>
        /// Gets media records for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>List of media records.</returns>
        Task<List<MediaRecord>> GetMediaByVirtualKeyAsync(int virtualKeyId);

        /// <summary>
        /// Manually triggers cleanup of expired media.
        /// </summary>
        /// <returns>Number of media files cleaned up.</returns>
        Task<int> CleanupExpiredMediaAsync();

        /// <summary>
        /// Manually triggers cleanup of orphaned media.
        /// </summary>
        /// <returns>Number of orphaned media files cleaned up.</returns>
        Task<int> CleanupOrphanedMediaAsync();

        /// <summary>
        /// Manually triggers pruning of old media.
        /// </summary>
        /// <param name="daysToKeep">Number of days to keep media files.</param>
        /// <returns>Number of media files pruned.</returns>
        Task<int> PruneOldMediaAsync(int daysToKeep);

        /// <summary>
        /// Deletes a specific media record.
        /// </summary>
        /// <param name="mediaId">The ID of the media record.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        Task<bool> DeleteMediaAsync(Guid mediaId);

        /// <summary>
        /// Gets media records by storage key pattern.
        /// </summary>
        /// <param name="storageKeyPattern">Pattern to match storage keys.</param>
        /// <returns>List of matching media records.</returns>
        Task<List<MediaRecord>> SearchMediaByStorageKeyAsync(string storageKeyPattern);

        /// <summary>
        /// Gets storage statistics grouped by provider.
        /// </summary>
        /// <returns>Dictionary of provider names to storage size.</returns>
        Task<Dictionary<string, long>> GetStorageStatsByProviderAsync();

        /// <summary>
        /// Gets storage statistics grouped by media type.
        /// </summary>
        /// <returns>Dictionary of media types to storage size.</returns>
        Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync();
    }
}