using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for ProviderKeyCredential operations
    /// </summary>
    public interface IProviderKeyCredentialRepository
    {
        /// <summary>
        /// Get all key credentials for a provider
        /// </summary>
        Task<List<ProviderKeyCredential>> GetByProviderIdAsync(int providerCredentialId);

        /// <summary>
        /// Get a specific key credential by ID
        /// </summary>
        Task<ProviderKeyCredential?> GetByIdAsync(int id);

        /// <summary>
        /// Get the primary key credential for a provider
        /// </summary>
        Task<ProviderKeyCredential?> GetPrimaryKeyAsync(int providerCredentialId);

        /// <summary>
        /// Get all enabled key credentials for a provider
        /// </summary>
        Task<List<ProviderKeyCredential>> GetEnabledKeysByProviderIdAsync(int providerCredentialId);

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
        Task<bool> SetPrimaryKeyAsync(int providerCredentialId, int keyId);

        /// <summary>
        /// Check if a provider has any key credentials
        /// </summary>
        Task<bool> HasKeyCredentialsAsync(int providerCredentialId);

        /// <summary>
        /// Count key credentials for a provider
        /// </summary>
        Task<int> CountByProviderIdAsync(int providerCredentialId);
    }
}