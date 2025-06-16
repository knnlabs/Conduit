using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the provider health service interface with the Admin API client
    /// </summary>
    public class ProviderHealthServiceAdapter
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderHealthServiceAdapter> _logger;

        public ProviderHealthServiceAdapter(IAdminApiClient adminApiClient, ILogger<ProviderHealthServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all provider health configurations
        /// </summary>
        /// <returns>Collection of provider health configurations</returns>
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthConfigurationDto>> GetAllConfigurationsAsync()
        {
            return await _adminApiClient.GetAllProviderHealthConfigurationsAsync();
        }

        /// <summary>
        /// Gets a provider health configuration by provider name
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider health configuration or null if not found</returns>
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> GetConfigurationByProviderNameAsync(string providerName)
        {
            return await _adminApiClient.GetProviderHealthConfigurationByNameAsync(providerName);
        }

        /// <summary>
        /// Creates a new provider health configuration
        /// </summary>
        /// <param name="config">The configuration to create</param>
        /// <returns>The created configuration</returns>
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> CreateConfigurationAsync(ConfigDTOs.CreateProviderHealthConfigurationDto config)
        {
            return await _adminApiClient.CreateProviderHealthConfigurationAsync(config);
        }

        /// <summary>
        /// Updates a provider health configuration
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <param name="config">The updated configuration</param>
        /// <returns>The updated configuration</returns>
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> UpdateConfigurationAsync(string providerName, ConfigDTOs.UpdateProviderHealthConfigurationDto config)
        {
            return await _adminApiClient.UpdateProviderHealthConfigurationAsync(providerName, config);
        }

        /// <summary>
        /// Deletes a provider health configuration
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteConfigurationAsync(string providerName)
        {
            return await _adminApiClient.DeleteProviderHealthConfigurationAsync(providerName);
        }

        /// <summary>
        /// Gets provider health records
        /// </summary>
        /// <param name="providerName">Optional provider name to filter records</param>
        /// <returns>Collection of provider health records</returns>
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthRecordDto>> GetRecordsAsync(string? providerName = null)
        {
            return await _adminApiClient.GetProviderHealthRecordsAsync(providerName);
        }

        /// <summary>
        /// Gets provider health summary
        /// </summary>
        /// <returns>Collection of provider health summaries</returns>
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthSummaryDto>> GetHealthSummaryAsync()
        {
            return await _adminApiClient.GetProviderHealthSummaryAsync();
        }
    }
}