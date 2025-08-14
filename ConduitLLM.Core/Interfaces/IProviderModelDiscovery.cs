using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for provider-specific model discovery.
    /// </summary>
    public interface IProviderModelDiscovery
    {
        /// <summary>
        /// Discovers models for a specific provider instance.
        /// </summary>
        /// <param name="Provider">The provider credential containing configuration.</param>
        /// <param name="httpClient">HTTP client for API calls.</param>
        /// <param name="apiKey">API key for the provider (optional, can be retrieved from credential).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models.</returns>
        Task<List<DiscoveredModel>> DiscoverModelsAsync(
            Provider Provider, 
            HttpClient httpClient,
            string? apiKey = null, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if discovery is supported for the given provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>True if discovery is supported.</returns>
        bool SupportsDiscovery(ProviderType providerType);
    }
}