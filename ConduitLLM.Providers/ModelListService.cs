using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Service for retrieving available models from LLM providers.
    /// </summary>
    public class ModelListService
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ModelListService> _logger;
        
        // Default cache duration - 1 hour is reasonable for model lists
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelListService"/> class.
        /// </summary>
        /// <param name="clientFactory">Factory for creating provider-specific LLM clients.</param>
        /// <param name="cache">Memory cache for storing model lists.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ModelListService(
            ILLMClientFactory clientFactory,
            IMemoryCache cache,
            ILogger<ModelListService> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a list of available model IDs from a provider.
        /// </summary>
        /// <param name="providerCredential">The provider credentials to use.</param>
        /// <param name="forceRefresh">Whether to bypass cache and force a refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available model IDs.</returns>
        public async Task<List<string>> GetModelsForProviderAsync(
            ProviderCredentials providerCredential,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            // Create a cache key based on provider name
            string cacheKey = $"provider_models:{providerCredential.ProviderName}";
            
            // Try to get from cache if not forcing a refresh
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<string>? cachedModels) && cachedModels != null)
            {
                _logger.LogDebug("Returning cached models for provider {ProviderName}", 
                    providerCredential.ProviderName);
                return cachedModels;
            }

            try
            {
                _logger.LogInformation("Fetching models from provider {ProviderName}", 
                    providerCredential.ProviderName);
                
                // Create a client using the existing factory
                var client = _clientFactory.GetClientByProvider(providerCredential.ProviderName);
                
                // Get models from the provider API
                var models = await client.ListModelsAsync(
                    providerCredential.ApiKey, 
                    cancellationToken);
                
                // Cache the results
                _cache.Set(cacheKey, models, _cacheDuration);
                
                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models for provider {ProviderName}", 
                    providerCredential.ProviderName);
                throw;
            }
        }
    }
}