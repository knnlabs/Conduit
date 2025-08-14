using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for provider instance-specific model discovery.
    /// </summary>
    public interface IProviderInstanceModelDiscovery
    {
        /// <summary>
        /// Discovers models for a specific provider instance.
        /// </summary>
        /// <param name="providerId">The provider instance ID.</param>
        /// <param name="providerType">The provider type enum.</param>
        /// <param name="httpClient">HTTP client for API calls.</param>
        /// <param name="apiKey">API key for the provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models.</returns>
        Task<List<DiscoveredModel>> DiscoverModelsAsync(
            int providerId,
            ProviderType providerType,
            HttpClient httpClient,
            string? apiKey, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if discovery is supported for the given provider type.
        /// </summary>
        /// <param name="providerType">The provider type enum.</param>
        /// <returns>True if discovery is supported.</returns>
        bool SupportsDiscovery(ProviderType providerType);
    }
}