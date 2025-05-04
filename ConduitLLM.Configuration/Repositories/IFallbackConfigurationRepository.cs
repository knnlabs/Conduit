using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing fallback configurations
    /// </summary>
    public interface IFallbackConfigurationRepository
    {
        /// <summary>
        /// Gets a fallback configuration by ID
        /// </summary>
        /// <param name="id">The fallback configuration ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The fallback configuration entity or null if not found</returns>
        Task<FallbackConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the active fallback configuration
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The active fallback configuration or null if none found</returns>
        Task<FallbackConfigurationEntity?> GetActiveConfigAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all fallback configurations
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all fallback configurations</returns>
        Task<List<FallbackConfigurationEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a new fallback configuration
        /// </summary>
        /// <param name="fallbackConfig">The fallback configuration to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created fallback configuration</returns>
        Task<Guid> CreateAsync(FallbackConfigurationEntity fallbackConfig, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates a fallback configuration
        /// </summary>
        /// <param name="fallbackConfig">The fallback configuration to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(FallbackConfigurationEntity fallbackConfig, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Activates a fallback configuration and deactivates all others
        /// </summary>
        /// <param name="id">The ID of the fallback configuration to activate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the activation was successful, false otherwise</returns>
        Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a fallback configuration
        /// </summary>
        /// <param name="id">The ID of the fallback configuration to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets fallback model mappings for a fallback configuration
        /// </summary>
        /// <param name="fallbackConfigId">The fallback configuration ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of fallback model mappings for the specified configuration</returns>
        Task<List<FallbackModelMappingEntity>> GetMappingsAsync(Guid fallbackConfigId, CancellationToken cancellationToken = default);
    }
}