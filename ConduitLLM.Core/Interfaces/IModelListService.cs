using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for retrieving available models from LLM providers.
    /// </summary>
    public interface IModelListService
    {
        /// <summary>
        /// Gets a list of available model IDs from a provider.
        /// </summary>
        /// <param name="providerCredential">The provider credentials to use.</param>
        /// <param name="forceRefresh">Whether to bypass cache and force a refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available model IDs.</returns>
        Task<List<string>> GetModelsForProviderAsync(
            ProviderCredentials providerCredential,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
    }
}