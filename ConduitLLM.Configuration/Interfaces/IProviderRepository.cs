using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing providers
    /// </summary>
    public interface IProviderRepository
    {
        /// <summary>
        /// Gets a provider by ID
        /// </summary>
        /// <param name="id">The provider ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The provider entity or null if not found</returns>
        Task<Provider?> GetByIdAsync(int id, CancellationToken cancellationToken = default);


        /// <summary>
        /// Gets all providers
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all providers</returns>
        Task<List<Provider>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <param name="provider">The provider to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created provider</returns>
        Task<int> CreateAsync(Provider provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a provider
        /// </summary>
        /// <param name="provider">The provider to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(Provider provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
