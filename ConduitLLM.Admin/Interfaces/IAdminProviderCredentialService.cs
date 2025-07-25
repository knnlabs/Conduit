using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing provider credentials through the Admin API
    /// </summary>
    public interface IAdminProviderCredentialService
    {
        /// <summary>
        /// Gets all provider credentials
        /// </summary>
        /// <returns>List of all provider credentials</returns>
        Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync();

        /// <summary>
        /// Gets a provider credential by ID
        /// </summary>
        /// <param name="id">The ID of the provider credential to get</param>
        /// <returns>The provider credential, or null if not found</returns>
        Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id);

        /// <summary>
        /// Gets a provider credential by provider name
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider credential, or null if not found</returns>
        Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName);

        /// <summary>
        /// Gets a list of all provider names with their IDs
        /// </summary>
        /// <returns>List of provider data</returns>
        Task<IEnumerable<ProviderDataDto>> GetAllProviderNamesAsync();

        /// <summary>
        /// Creates a new provider credential
        /// </summary>
        /// <param name="providerCredential">The provider credential to create</param>
        /// <returns>The created provider credential</returns>
        Task<ProviderCredentialDto> CreateProviderCredentialAsync(CreateProviderCredentialDto providerCredential);

        /// <summary>
        /// Updates a provider credential
        /// </summary>
        /// <param name="providerCredential">The provider credential to update</param>
        /// <returns>True if update was successful, false if the provider credential was not found</returns>
        Task<bool> UpdateProviderCredentialAsync(UpdateProviderCredentialDto providerCredential);

        /// <summary>
        /// Deletes a provider credential
        /// </summary>
        /// <param name="id">The ID of the provider credential to delete</param>
        /// <returns>True if deletion was successful, false if the provider credential was not found</returns>
        Task<bool> DeleteProviderCredentialAsync(int id);

        /// <summary>
        /// Tests the connection to a provider using the specified credentials
        /// </summary>
        /// <param name="providerCredential">The provider credential to test</param>
        /// <returns>A result indicating success or failure with error details</returns>
        Task<ProviderConnectionTestResultDto> TestProviderConnectionAsync(ProviderCredentialDto providerCredential);

        /// <summary>
        /// Gets all key credentials for a specific provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <returns>List of key credentials for the provider</returns>
        Task<IEnumerable<ProviderKeyCredentialDto>> GetProviderKeyCredentialsAsync(int providerId);

        /// <summary>
        /// Gets a specific key credential
        /// </summary>
        /// <param name="keyId">The ID of the key credential</param>
        /// <returns>The key credential, or null if not found</returns>
        Task<ProviderKeyCredentialDto?> GetProviderKeyCredentialAsync(int keyId);

        /// <summary>
        /// Creates a new key credential for a provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyCredential">The key credential to create</param>
        /// <returns>The created key credential</returns>
        Task<ProviderKeyCredentialDto> CreateProviderKeyCredentialAsync(int providerId, CreateProviderKeyCredentialDto keyCredential);

        /// <summary>
        /// Updates a key credential
        /// </summary>
        /// <param name="keyId">The ID of the key credential to update</param>
        /// <param name="keyCredential">The updated key credential data</param>
        /// <returns>True if update was successful, false if the key credential was not found</returns>
        Task<bool> UpdateProviderKeyCredentialAsync(int keyId, UpdateProviderKeyCredentialDto keyCredential);

        /// <summary>
        /// Deletes a key credential
        /// </summary>
        /// <param name="keyId">The ID of the key credential to delete</param>
        /// <returns>True if deletion was successful, false if the key credential was not found</returns>
        Task<bool> DeleteProviderKeyCredentialAsync(int keyId);

        /// <summary>
        /// Sets a key as the primary key for a provider
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to set as primary</param>
        /// <returns>True if successful, false if the key credential was not found</returns>
        Task<bool> SetPrimaryKeyAsync(int providerId, int keyId);

        /// <summary>
        /// Tests a specific key credential
        /// </summary>
        /// <param name="providerId">The ID of the provider</param>
        /// <param name="keyId">The ID of the key to test</param>
        /// <returns>A result indicating success or failure with error details</returns>
        Task<ProviderConnectionTestResultDto> TestProviderKeyCredentialAsync(int providerId, int keyId);
    }

    /// <summary>
    /// Result of a provider connection test
    /// </summary>
    public class ProviderConnectionTestResult
    {
        /// <summary>
        /// Whether the connection test was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message if the test failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Response time in milliseconds for the connection test
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Additional details about the connection test
        /// </summary>
        public string? Details { get; set; }
    }
}
