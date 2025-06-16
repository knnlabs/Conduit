using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the provider credential service interface with the Admin API client
    /// </summary>
    public class ProviderCredentialServiceAdapter
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderCredentialServiceAdapter> _logger;

        public ProviderCredentialServiceAdapter(IAdminApiClient adminApiClient, ILogger<ProviderCredentialServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all provider credentials
        /// </summary>
        /// <returns>Collection of provider credentials</returns>
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync()
        {
            return await _adminApiClient.GetAllProviderCredentialsAsync();
        }

        /// <summary>
        /// Gets a provider credential by ID
        /// </summary>
        /// <param name="id">The credential ID</param>
        /// <returns>The provider credential or null if not found</returns>
        public async Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id)
        {
            return await _adminApiClient.GetProviderCredentialByIdAsync(id);
        }

        /// <summary>
        /// Gets a provider credential by name
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>The provider credential or null if not found</returns>
        public async Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName)
        {
            return await _adminApiClient.GetProviderCredentialByNameAsync(providerName);
        }

        /// <summary>
        /// Creates a new provider credential
        /// </summary>
        /// <param name="credential">The credential to create</param>
        /// <returns>The created credential</returns>
        public async Task<ProviderCredentialDto?> CreateProviderCredentialAsync(CreateProviderCredentialDto credential)
        {
            return await _adminApiClient.CreateProviderCredentialAsync(credential);
        }

        /// <summary>
        /// Updates a provider credential
        /// </summary>
        /// <param name="id">The credential ID</param>
        /// <param name="credential">The updated credential data</param>
        /// <returns>The updated credential</returns>
        public async Task<ProviderCredentialDto?> UpdateProviderCredentialAsync(int id, UpdateProviderCredentialDto credential)
        {
            return await _adminApiClient.UpdateProviderCredentialAsync(id, credential);
        }

        /// <summary>
        /// Deletes a provider credential
        /// </summary>
        /// <param name="id">The credential ID</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteProviderCredentialAsync(int id)
        {
            return await _adminApiClient.DeleteProviderCredentialAsync(id);
        }

        /// <summary>
        /// Tests a provider connection
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>Test result</returns>
        public async Task<ProviderConnectionTestResultDto?> TestProviderConnectionAsync(string providerName)
        {
            return await _adminApiClient.TestProviderConnectionAsync(providerName);
        }

        /// <summary>
        /// Gets credentials for a provider as a dictionary
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>Dictionary of credential properties</returns>
        public async Task<Dictionary<string, string>> GetCredentialsForProviderAsync(string providerName)
        {
            try
            {
                var credential = await _adminApiClient.GetProviderCredentialByNameAsync(providerName);
                if (credential == null)
                {
                    return new Dictionary<string, string>();
                }

                var result = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(credential.ApiKey))
                {
                    result["api_key"] = credential.ApiKey;
                }

                if (!string.IsNullOrEmpty(credential.ApiBase))
                {
                    result["api_base"] = credential.ApiBase;
                }

                if (!string.IsNullOrEmpty(credential.OrgId))
                {
                    result["organization_id"] = credential.OrgId;
                }

                if (!string.IsNullOrEmpty(credential.ProjectId))
                {
                    result["project_id"] = credential.ProjectId;
                }

                if (!string.IsNullOrEmpty(credential.Region))
                {
                    result["region"] = credential.Region;
                }

                if (!string.IsNullOrEmpty(credential.EndpointUrl))
                {
                    result["endpoint_url"] = credential.EndpointUrl;
                }

                if (!string.IsNullOrEmpty(credential.DeploymentName))
                {
                    result["deployment_name"] = credential.DeploymentName;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credentials for provider {ProviderName}", providerName);
                return new Dictionary<string, string>();
            }
        }
    }
}