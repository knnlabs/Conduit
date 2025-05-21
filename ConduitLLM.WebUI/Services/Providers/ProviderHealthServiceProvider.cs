using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IProviderHealthService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class ProviderHealthServiceProvider : IProviderHealthService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderHealthServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ProviderHealthServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<ProviderHealthServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> CreateConfigurationAsync(ConfigDTOs.CreateProviderHealthConfigurationDto config)
        {
            try
            {
                return await _adminApiClient.CreateProviderHealthConfigurationAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider health configuration for provider {ProviderName}", config.ProviderName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConfigurationAsync(string providerName)
        {
            try
            {
                return await _adminApiClient.DeleteProviderHealthConfigurationAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider health configuration for provider {ProviderName}", providerName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthConfigurationDto>> GetAllConfigurationsAsync()
        {
            try
            {
                return await _adminApiClient.GetAllProviderHealthConfigurationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all provider health configurations");
                return Enumerable.Empty<ConfigDTOs.ProviderHealthConfigurationDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> GetConfigurationByNameAsync(string providerName)
        {
            try
            {
                return await _adminApiClient.GetProviderHealthConfigurationByNameAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health configuration for provider {ProviderName}", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthRecordDto>> GetHealthRecordsAsync(string? providerName = null)
        {
            try
            {
                return await _adminApiClient.GetProviderHealthRecordsAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health records {ProviderFilter}", 
                    providerName != null ? $"for provider {providerName}" : "for all providers");
                return Enumerable.Empty<ConfigDTOs.ProviderHealthRecordDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthSummaryDto>> GetHealthSummaryAsync()
        {
            try
            {
                return await _adminApiClient.GetProviderHealthSummaryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health summary");
                return Enumerable.Empty<ConfigDTOs.ProviderHealthSummaryDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> UpdateConfigurationAsync(string providerName, ConfigDTOs.UpdateProviderHealthConfigurationDto config)
        {
            try
            {
                return await _adminApiClient.UpdateProviderHealthConfigurationAsync(providerName, config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider health configuration for provider {ProviderName}", providerName);
                return null;
            }
        }
    }
}