using ConduitLLM.Configuration.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for managing provider credentials
    /// </summary>
    public interface IProviderCredentialService
    {
        /// <summary>
        /// Gets all provider credentials.
        /// </summary>
        /// <returns>Collection of provider credentials.</returns>
        Task<IEnumerable<ProviderCredentialDto>> GetAllAsync();

        /// <summary>
        /// Gets a provider credential by ID.
        /// </summary>
        /// <param name="id">The ID of the provider credential to retrieve.</param>
        /// <returns>The provider credential, or null if not found.</returns>
        Task<ProviderCredentialDto?> GetByIdAsync(int id);

        /// <summary>
        /// Gets a provider credential by provider name.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider credential, or null if not found.</returns>
        Task<ProviderCredentialDto?> GetByProviderNameAsync(string providerName);

        /// <summary>
        /// Creates a new provider credential.
        /// </summary>
        /// <param name="credential">The provider credential to create.</param>
        /// <returns>The created provider credential.</returns>
        Task<ProviderCredentialDto?> CreateAsync(CreateProviderCredentialDto credential);

        /// <summary>
        /// Updates a provider credential.
        /// </summary>
        /// <param name="id">The ID of the provider credential to update.</param>
        /// <param name="credential">The updated provider credential.</param>
        /// <returns>The updated provider credential, or null if the update failed.</returns>
        Task<ProviderCredentialDto?> UpdateAsync(int id, UpdateProviderCredentialDto credential);

        /// <summary>
        /// Deletes a provider credential.
        /// </summary>
        /// <param name="id">The ID of the provider credential to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Tests a provider connection.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>A result indicating whether the connection was successful.</returns>
        Task<ProviderConnectionTestResultDto?> TestConnectionAsync(string providerName);

        /// <summary>
        /// Tests a provider connection with given credentials (without saving).
        /// </summary>
        /// <param name="providerCredential">The provider credentials to test.</param>
        /// <returns>A result indicating whether the connection was successful.</returns>
        Task<ProviderConnectionTestResultDto?> TestProviderConnectionWithCredentialsAsync(ProviderCredentialDto providerCredential);
    }
}