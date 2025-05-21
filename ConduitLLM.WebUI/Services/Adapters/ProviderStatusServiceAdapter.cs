using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Service adapter for checking provider status using Admin API
    /// </summary>
    public class ProviderStatusServiceAdapter : IProviderStatusService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderStatusServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderStatusServiceAdapter class
        /// </summary>
        /// <param name="adminApiClient">Admin API client for accessing provider status</param>
        /// <param name="logger">Logger for tracking service operations</param>
        public ProviderStatusServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<ProviderStatusServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking status of all providers via Admin API");
                return await _adminApiClient.CheckAllProvidersStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of all providers");
                return new Dictionary<string, ProviderStatus>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(ProviderCredentialDto provider, CancellationToken cancellationToken = default)
        {
            return await CheckProviderStatusAsync(provider.ProviderName, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking status of provider {ProviderName} via Admin API", providerName);
                return await _adminApiClient.CheckProviderStatusAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of provider {ProviderName}", providerName);
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Unknown,
                    StatusMessage = $"Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }
    }
}