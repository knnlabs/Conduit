using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IProviderHealthService"/> using the Admin API client.
    /// </summary>
    public class ProviderHealthServiceAdapter : IProviderHealthService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderHealthServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ProviderHealthServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<ProviderHealthServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthConfigurationDto>> GetAllConfigurationsAsync()
        {
            return await _adminApiClient.GetAllProviderHealthConfigurationsAsync();
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> GetConfigurationByNameAsync(string providerName)
        {
            return await _adminApiClient.GetProviderHealthConfigurationByNameAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> CreateConfigurationAsync(CreateProviderHealthConfigurationDto config)
        {
            return await _adminApiClient.CreateProviderHealthConfigurationAsync(config);
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> UpdateConfigurationAsync(string providerName, UpdateProviderHealthConfigurationDto config)
        {
            return await _adminApiClient.UpdateProviderHealthConfigurationAsync(providerName, config);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConfigurationAsync(string providerName)
        {
            return await _adminApiClient.DeleteProviderHealthConfigurationAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthRecordDto>> GetHealthRecordsAsync(string? providerName = null)
        {
            return await _adminApiClient.GetProviderHealthRecordsAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthSummaryDto>> GetHealthSummaryAsync()
        {
            return await _adminApiClient.GetProviderHealthSummaryAsync();
        }

        // Legacy method implementations that delegate to the new interface methods
        /// <summary>
        /// Legacy method that delegates to <see cref="GetConfigurationByNameAsync"/>.
        /// </summary>
        public async Task<ProviderHealthConfigurationDto?> GetConfigurationByProviderNameAsync(string providerName)
        {
            return await GetConfigurationByNameAsync(providerName);
        }

        /// <summary>
        /// Legacy method that delegates to <see cref="GetHealthRecordsAsync"/>.
        /// </summary>
        public async Task<IEnumerable<ProviderHealthRecordDto>> GetRecordsAsync(string? providerName = null)
        {
            return await GetHealthRecordsAsync(providerName);
        }
    }
}