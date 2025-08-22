using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for ProviderKeyCredential operations
    /// </summary>
    public interface IProviderKeyCredentialRepository
    {
        /// <summary>
        /// Get all key credentials across all providers
        /// </summary>
        Task<List<ProviderKeyCredential>> GetAllAsync();

        /// <summary>
        /// Get all key credentials for a provider
        /// </summary>
        Task<List<ProviderKeyCredential>> GetByProviderIdAsync(int ProviderId);

        /// <summary>
        /// Get a specific key credential by ID
        /// </summary>
        Task<ProviderKeyCredential?> GetByIdAsync(int id);

        /// <summary>
        /// Get the primary key credential for a provider
        /// </summary>
        Task<ProviderKeyCredential?> GetPrimaryKeyAsync(int ProviderId);

        /// <summary>
        /// Get all enabled key credentials for a provider
        /// </summary>
        Task<List<ProviderKeyCredential>> GetEnabledKeysByProviderIdAsync(int ProviderId);

        /// <summary>
        /// Create a new key credential
        /// </summary>
        Task<ProviderKeyCredential> CreateAsync(ProviderKeyCredential keyCredential);

        /// <summary>
        /// Update an existing key credential
        /// </summary>
        Task<bool> UpdateAsync(ProviderKeyCredential keyCredential);

        /// <summary>
        /// Delete a key credential
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Set a key as primary (and unset others)
        /// </summary>
        Task<bool> SetPrimaryKeyAsync(int ProviderId, int keyId);

        /// <summary>
        /// Check if a provider has any key credentials
        /// </summary>
        Task<bool> HasKeyCredentialsAsync(int ProviderId);

        /// <summary>
        /// Count key credentials for a provider
        /// </summary>
        Task<int> CountByProviderIdAsync(int ProviderId);
    }
}