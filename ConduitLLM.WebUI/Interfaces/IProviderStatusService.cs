using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for checking the status of LLM providers
    /// </summary>
    public interface IProviderStatusService
    {
        /// <summary>
        /// Checks the status of all configured providers
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A dictionary mapping provider names to their status</returns>
        Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks the status of a specific provider
        /// </summary>
        /// <param name="provider">The provider credentials to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The status of the provider</returns>
        Task<ProviderStatus> CheckProviderStatusAsync(ProviderCredentialDto provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks the status of a provider by name
        /// </summary>
        /// <param name="providerName">The name of the provider to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The status of the provider</returns>
        Task<ProviderStatus> CheckProviderStatusAsync(string providerName, CancellationToken cancellationToken = default);
    }
}
