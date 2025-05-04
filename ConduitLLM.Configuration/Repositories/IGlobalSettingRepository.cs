using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing global settings
    /// </summary>
    public interface IGlobalSettingRepository
    {
        /// <summary>
        /// Gets a global setting by ID
        /// </summary>
        /// <param name="id">The global setting ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The global setting entity or null if not found</returns>
        Task<GlobalSetting?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The global setting entity or null if not found</returns>
        Task<GlobalSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all global settings
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all global settings</returns>
        Task<List<GlobalSetting>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a new global setting
        /// </summary>
        /// <param name="globalSetting">The global setting to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created global setting</returns>
        Task<int> CreateAsync(GlobalSetting globalSetting, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates a global setting
        /// </summary>
        /// <param name="globalSetting">The global setting to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(GlobalSetting globalSetting, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates or creates a global setting
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="value">The setting value</param>
        /// <param name="description">Optional description</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        Task<bool> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a global setting
        /// </summary>
        /// <param name="id">The ID of the global setting to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a global setting by key
        /// </summary>
        /// <param name="key">The key of the global setting to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default);
    }
}