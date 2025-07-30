using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for provider-specific model discovery.
    /// </summary>
    public interface IProviderModelDiscovery
    {
        /// <summary>
        /// Discovers models for a specific provider.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="httpClient">HTTP client for API calls.</param>
        /// <param name="apiKey">API key for the provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models.</returns>
        Task<List<DiscoveredModel>> DiscoverModelsAsync(
            string providerName, 
            HttpClient httpClient,
            string? apiKey, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if discovery is supported for the given provider.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <returns>True if discovery is supported.</returns>
        bool SupportsDiscovery(string providerName);
    }
}