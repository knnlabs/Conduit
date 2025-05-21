using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IProviderCredentialService that uses the AdminApiClient to interact with the Admin API.
    /// </summary>
    public class ProviderCredentialServiceProvider : IProviderCredentialService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderCredentialServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderCredentialServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ProviderCredentialServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<ProviderCredentialServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllAsync()
        {
            try
            {
                return await _adminApiClient.GetAllProviderCredentialsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all provider credentials from Admin API");
                return new List<ProviderCredentialDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetByIdAsync(int id)
        {
            try
            {
                return await _adminApiClient.GetProviderCredentialByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider credential with ID {Id} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetByProviderNameAsync(string providerName)
        {
            try
            {
                return await _adminApiClient.GetProviderCredentialByNameAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider credential for provider {ProviderName} from Admin API", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> CreateAsync(CreateProviderCredentialDto credential)
        {
            try
            {
                return await _adminApiClient.CreateProviderCredentialAsync(credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential for provider {ProviderName} using Admin API", credential.ProviderName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> UpdateAsync(int id, UpdateProviderCredentialDto credential)
        {
            try
            {
                return await _adminApiClient.UpdateProviderCredentialAsync(id, credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {Id} using Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                return await _adminApiClient.DeleteProviderCredentialAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {Id} using Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderConnectionTestResultDto?> TestConnectionAsync(string providerName)
        {
            try
            {
                return await _adminApiClient.TestProviderConnectionAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider {ProviderName} using Admin API", providerName);
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Error testing connection: {ex.Message}"
                };
            }
        }
    }
}