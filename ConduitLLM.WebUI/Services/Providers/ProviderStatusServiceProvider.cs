using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Service provider for checking the status of LLM providers using the Admin API.
    /// </summary>
    public class ProviderStatusServiceProvider : IProviderStatusService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ProviderStatusServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderStatusServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ProviderStatusServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<ProviderStatusServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _adminApiClient.CheckAllProvidersStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of all providers via Admin API");
                
                // Return an empty dictionary in case of error
                return new Dictionary<string, ProviderStatus>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(ProviderCredentialDto provider, CancellationToken cancellationToken = default)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return await CheckProviderStatusAsync(provider.ProviderName, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(providerName))
                {
                    throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
                }

                return await _adminApiClient.CheckProviderStatusAsync(providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of provider {ProviderName} via Admin API", providerName);
                
                // Return an offline status with the error message
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Offline,
                    StatusMessage = $"Error checking provider status: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = "APIError"
                };
            }
        }
    }
}