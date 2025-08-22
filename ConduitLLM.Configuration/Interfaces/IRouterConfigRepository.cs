using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing router configurations
    /// </summary>
    public interface IRouterConfigRepository
    {
        /// <summary>
        /// Gets a router configuration by ID
        /// </summary>
        /// <param name="id">The router configuration ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The router configuration entity or null if not found</returns>
        Task<RouterConfigEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the active router configuration
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The active router configuration or null if none found</returns>
        Task<RouterConfigEntity?> GetActiveConfigAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all router configurations
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all router configurations</returns>
        Task<List<RouterConfigEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new router configuration
        /// </summary>
        /// <param name="routerConfig">The router configuration to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created router configuration</returns>
        Task<int> CreateAsync(RouterConfigEntity routerConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a router configuration
        /// </summary>
        /// <param name="routerConfig">The router configuration to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(RouterConfigEntity routerConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a router configuration and deactivates all others
        /// </summary>
        /// <param name="id">The ID of the router configuration to activate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the activation was successful, false otherwise</returns>
        Task<bool> ActivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a router configuration
        /// </summary>
        /// <param name="id">The ID of the router configuration to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
