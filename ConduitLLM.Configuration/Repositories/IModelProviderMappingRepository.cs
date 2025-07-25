using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing model provider mappings in the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Model provider mappings are a key part of the Conduit routing system. They define
    /// how model aliases (user-friendly names) map to specific provider models, allowing
    /// for model abstraction and seamless provider switching.
    /// </para>
    /// <para>
    /// Key features of this repository include:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>CRUD operations for model provider mapping entities</description></item>
    ///   <item><description>Lookup by model alias to find the appropriate provider and model</description></item>
    ///   <item><description>Filtering by provider to get all mappings for a specific provider</description></item>
    /// </list>
    /// <para>
    /// This interface follows the repository pattern, abstracting the data access layer
    /// and providing a clean, domain-focused API for model mapping management.
    /// </para>
    /// </remarks>
    public interface IModelProviderMappingRepository
    {
        /// <summary>
        /// Gets a model provider mapping by ID
        /// </summary>
        /// <param name="id">The model provider mapping ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model provider mapping entity or null if not found</returns>
        Task<Entities.ModelProviderMapping?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a model provider mapping by model alias
        /// </summary>
        /// <param name="modelName">The model alias</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model provider mapping entity or null if not found</returns>
        Task<Entities.ModelProviderMapping?> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model provider mappings
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all model provider mappings</returns>
        Task<List<Entities.ModelProviderMapping>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model provider mappings for a specific provider
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of model provider mappings for the specified provider</returns>
        Task<List<Entities.ModelProviderMapping>> GetByProviderAsync(string providerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model provider mappings for a specific provider
        /// </summary>
        /// <param name="providerType">The provider type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of model provider mappings for the specified provider</returns>
        Task<List<Entities.ModelProviderMapping>> GetByProviderAsync(ProviderType providerType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new model provider mapping
        /// </summary>
        /// <param name="modelProviderMapping">The model provider mapping to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created model provider mapping</returns>
        Task<int> CreateAsync(Entities.ModelProviderMapping modelProviderMapping, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a model provider mapping
        /// </summary>
        /// <param name="modelProviderMapping">The model provider mapping to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(Entities.ModelProviderMapping modelProviderMapping, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a model provider mapping
        /// </summary>
        /// <param name="id">The ID of the model provider mapping to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
