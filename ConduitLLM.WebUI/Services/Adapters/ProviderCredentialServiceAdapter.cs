using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IProviderCredentialService"/> using the Admin API client.
    /// </summary>
    public class ProviderCredentialServiceAdapter : IProviderCredentialService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderCredentialServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderCredentialServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ProviderCredentialServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<ProviderCredentialServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllAsync()
        {
            return await _adminApiClient.GetAllProviderCredentialsAsync();
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetByIdAsync(int id)
        {
            return await _adminApiClient.GetProviderCredentialByIdAsync(id);
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetByProviderNameAsync(string providerName)
        {
            return await _adminApiClient.GetProviderCredentialByNameAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> CreateAsync(CreateProviderCredentialDto credential)
        {
            return await _adminApiClient.CreateProviderCredentialAsync(credential);
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> UpdateAsync(int id, UpdateProviderCredentialDto credential)
        {
            return await _adminApiClient.UpdateProviderCredentialAsync(id, credential);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            return await _adminApiClient.DeleteProviderCredentialAsync(id);
        }

        /// <inheritdoc />
        public async Task<ProviderConnectionTestResultDto?> TestConnectionAsync(string providerName)
        {
            return await _adminApiClient.TestProviderConnectionAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<IDictionary<string, string>> GetCredentialsForProviderAsync(string providerName)
        {
            try
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var credential = await _adminApiClient.GetProviderCredentialByNameAsync(providerName);
                if (credential == null)
                {
                    return result;
                }

                // Add all non-null credential properties to the dictionary
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

        // Legacy method implementations that delegate to the new interface methods
        /// <summary>
        /// Legacy method that delegates to <see cref="GetAllAsync"/>.
        /// </summary>
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync()
        {
            return await GetAllAsync();
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="GetByIdAsync"/>.
        /// </summary>
        public async Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="GetByProviderNameAsync"/>.
        /// </summary>
        public async Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName)
        {
            return await GetByProviderNameAsync(providerName);
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="CreateAsync"/>.
        /// </summary>
        public async Task<ProviderCredentialDto?> CreateProviderCredentialAsync(CreateProviderCredentialDto credential)
        {
            return await CreateAsync(credential);
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="UpdateAsync"/>.
        /// </summary>
        public async Task<ProviderCredentialDto?> UpdateProviderCredentialAsync(int id, UpdateProviderCredentialDto credential)
        {
            return await UpdateAsync(id, credential);
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="DeleteAsync"/>.
        /// </summary>
        public async Task<bool> DeleteProviderCredentialAsync(int id)
        {
            return await DeleteAsync(id);
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="TestConnectionAsync"/>.
        /// </summary>
        public async Task<ProviderConnectionTestResultDto?> TestProviderConnectionAsync(string providerName)
        {
            return await TestConnectionAsync(providerName);
        }
    }
}