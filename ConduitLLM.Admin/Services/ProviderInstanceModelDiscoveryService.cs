using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Implementation of provider instance-specific model discovery.
    /// </summary>
    public class ProviderInstanceModelDiscoveryService : IProviderInstanceModelDiscovery
    {
        private readonly ILogger<ProviderInstanceModelDiscoveryService> _logger;
        private readonly IProviderCredentialRepository _providerCredentialRepository;
        private readonly IProviderModelDiscovery _providerModelDiscovery;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderInstanceModelDiscoveryService"/> class.
        /// </summary>
        /// <param name="logger">The logger for diagnostics.</param>
        /// <param name="providerCredentialRepository">Repository for provider credentials.</param>
        /// <param name="providerModelDiscovery">The legacy provider model discovery service.</param>
        public ProviderInstanceModelDiscoveryService(
            ILogger<ProviderInstanceModelDiscoveryService> logger,
            IProviderCredentialRepository providerCredentialRepository,
            IProviderModelDiscovery providerModelDiscovery)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providerCredentialRepository = providerCredentialRepository ?? throw new ArgumentNullException(nameof(providerCredentialRepository));
            _providerModelDiscovery = providerModelDiscovery ?? throw new ArgumentNullException(nameof(providerModelDiscovery));
        }
        
        /// <inheritdoc/>
        public async Task<List<DiscoveredModel>> DiscoverModelsAsync(
            int providerId,
            ProviderType providerType,
            HttpClient httpClient,
            string? apiKey, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Discovering models for provider instance {ProviderId} of type {ProviderType}", providerId, providerType);
            
            // Get provider instance to access its name
            var providerInstance = await _providerCredentialRepository.GetByIdAsync(providerId);
            var providerName = providerInstance?.ProviderName ?? providerType.ToString();
            
            try
            {
                // Use the legacy discovery service for now, but with provider type
                var models = await _providerModelDiscovery.DiscoverModelsAsync(
                    providerType.ToString(), 
                    httpClient, 
                    apiKey, 
                    cancellationToken);
                
                // Update each model with the provider instance information
                foreach (var model in models)
                {
                    model.Provider = providerName; // Use the instance name instead of type
                    // We could add a ProviderId property to DiscoveredModel in the future
                }
                
                _logger.LogInformation("Discovered {Count} models for provider instance {ProviderId} ({ProviderName})", 
                    models.Count, providerId, providerName);
                
                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider instance {ProviderId} of type {ProviderType}", 
                    providerId, providerType);
                
                // Return empty list on error (no fallbacks)
                return new List<DiscoveredModel>();
            }
        }
        
        /// <inheritdoc/>
        public bool SupportsDiscovery(ProviderType providerType)
        {
            // Use the legacy service to check support
            return _providerModelDiscovery.SupportsDiscovery(providerType.ToString());
        }
    }
}