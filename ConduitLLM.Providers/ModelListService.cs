using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Service for retrieving available models from LLM providers.
    /// </summary>
    [Obsolete("External model discovery is no longer used. The ProviderModelsController now returns models from the local database based on provider type compatibility. This service will be removed in a future version.")]
    public class ModelListService : IModelListService
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
        /// <param name="provider">The provider entity.</param>
        /// <param name="keyCredential">The key credential to use for authentication.</param>
        /// <param name="forceRefresh">Whether to bypass cache and force a refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available model IDs.</returns>
        public async Task<List<string>> GetModelsForProviderAsync(
            Provider provider,
            ProviderKeyCredential keyCredential,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            // Create a cache key based on provider ID
            string cacheKey = $"provider_models:{provider.Id}";

            // Try to get from cache if not forcing a refresh
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<string>? cachedModels) && cachedModels != null)
            {
                _logger.LogDebug("Returning cached models for provider {ProviderName} (ID: {ProviderId})",
                    provider.ProviderName, provider.Id);
                return cachedModels;
            }

            try
            {
                _logger.LogInformation("Fetching models from provider {ProviderName} (ID: {ProviderId})",
                    provider.ProviderName, provider.Id);

                // Create a client using the provider ID
                var client = _clientFactory.GetClientByProviderId(provider.Id);

                // Get models from the provider API
                var models = await client.ListModelsAsync(
                    keyCredential.ApiKey,
                    cancellationToken);

                // Cache the results
                _cache.Set(cacheKey, models, _cacheDuration);

                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models for provider {ProviderName} (ID: {ProviderId})",
                    provider.ProviderName, provider.Id);
                throw;
            }
        }
    }
}
