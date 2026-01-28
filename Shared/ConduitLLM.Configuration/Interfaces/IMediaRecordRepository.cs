using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for media record operations.
    /// Extends IRepositoryBase for standard CRUD operations and adds domain-specific methods.
    /// </summary>
    public interface IMediaRecordRepository : IRepositoryBase<MediaRecord, Guid>
    {
        /// <summary>
        /// Gets a media record by its storage key.
        /// </summary>
        /// <param name="storageKey">The storage key of the media record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The media record if found, null otherwise.</returns>
        Task<MediaRecord?> GetByStorageKeyAsync(string storageKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all media records for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of media records for the virtual key ordered by created date descending.</returns>
        Task<List<MediaRecord>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets media records that have expired.
        /// </summary>
        /// <param name="currentTime">The current time to compare against.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of expired media records.</returns>
        Task<List<MediaRecord>> GetExpiredMediaAsync(DateTime currentTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets media records older than a specified date.
        /// </summary>
        /// <param name="cutoffDate">The cutoff date.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of old media records.</returns>
        Task<List<MediaRecord>> GetMediaOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets orphaned media records (where virtual key no longer exists).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of orphaned media records.</returns>
        Task<List<MediaRecord>> GetOrphanedMediaAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates access statistics for a media record.
        /// </summary>
        /// <param name="id">The ID of the media record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if updated successfully, false otherwise.</returns>
        Task<bool> UpdateAccessStatsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple media records.
        /// </summary>
        /// <param name="ids">The IDs of the media records to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the total storage size used by a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Total storage size in bytes.</returns>
        Task<long> GetTotalStorageSizeByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets storage statistics grouped by provider.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary of provider names to total storage size.</returns>
        Task<Dictionary<string, long>> GetStorageStatsByProviderAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets storage statistics grouped by media type.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary of media types to total storage size.</returns>
        Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the count of media records for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Count of media records.</returns>
        Task<int> GetCountByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default);
    }
}
