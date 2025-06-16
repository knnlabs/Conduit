using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Implementation of IProviderStatusService that delegates to AdminApiClient
    /// </summary>
    public class ProviderStatusService : IProviderStatusService
    {
        private readonly AdminApiClient _adminApiClient;
        private readonly IProviderCredentialService _providerCredentialService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderStatusService"/> class.
        /// </summary>
        /// <param name="adminApiClient">The admin API client</param>
        /// <param name="providerCredentialService">The provider credential service</param>
        public ProviderStatusService(AdminApiClient adminApiClient, IProviderCredentialService providerCredentialService)
        {
            _adminApiClient = adminApiClient;
            _providerCredentialService = providerCredentialService;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync(CancellationToken cancellationToken = default)
        {
            return await _adminApiClient.CheckAllProvidersStatusAsync();
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(ProviderCredentialDto provider, CancellationToken cancellationToken = default)
        {
            return await _adminApiClient.CheckProviderStatusAsync(provider.ProviderName);
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName, CancellationToken cancellationToken = default)
        {
            return await _adminApiClient.CheckProviderStatusAsync(providerName);
        }
    }
}
