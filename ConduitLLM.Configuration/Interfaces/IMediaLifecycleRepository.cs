using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for media lifecycle records
    /// </summary>
    public interface IMediaLifecycleRepository
    {
        /// <summary>
        /// Add a new media lifecycle record
        /// </summary>
        Task<MediaLifecycleRecord> AddAsync(MediaLifecycleRecord record);

        /// <summary>
        /// Get all media records for a virtual key
        /// </summary>
        Task<IList<MediaLifecycleRecord>> GetByVirtualKeyIdAsync(int virtualKeyId);

        /// <summary>
        /// Get media records that need cleanup (expired and not deleted)
        /// </summary>
        Task<IList<MediaLifecycleRecord>> GetExpiredMediaAsync(DateTime cutoffDate);

        /// <summary>
        /// Mark media as deleted
        /// </summary>
        Task<bool> MarkAsDeletedAsync(int recordId);

        /// <summary>
        /// Delete all media records for a virtual key
        /// </summary>
        Task<int> DeleteByVirtualKeyIdAsync(int virtualKeyId);

        /// <summary>
        /// Get total storage used by a virtual key
        /// </summary>
        Task<long> GetTotalStorageByVirtualKeyIdAsync(int virtualKeyId);

        /// <summary>
        /// Get media record by storage key
        /// </summary>
        Task<MediaLifecycleRecord?> GetByStorageKeyAsync(string storageKey);
    }
}