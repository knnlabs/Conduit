using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing provider credentials
    /// </summary>
    public interface IProviderCredentialRepository
    {
        /// <summary>
        /// Gets a provider credential by ID
        /// </summary>
        /// <param name="id">The provider credential ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The provider credential entity or null if not found</returns>
        Task<ProviderCredential?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a provider credential by provider name
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The provider credential entity or null if not found</returns>
        Task<ProviderCredential?> GetByProviderNameAsync(string providerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a provider credential by provider type
        /// </summary>
        /// <param name="providerType">The provider type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The provider credential entity or null if not found</returns>
        Task<ProviderCredential?> GetByProviderTypeAsync(ProviderType providerType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all provider credentials
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all provider credentials</returns>
        Task<List<ProviderCredential>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new provider credential
        /// </summary>
        /// <param name="providerCredential">The provider credential to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created provider credential</returns>
        Task<int> CreateAsync(ProviderCredential providerCredential, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a provider credential
        /// </summary>
        /// <param name="providerCredential">The provider credential to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(ProviderCredential providerCredential, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a provider credential
        /// </summary>
        /// <param name="id">The ID of the provider credential to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
