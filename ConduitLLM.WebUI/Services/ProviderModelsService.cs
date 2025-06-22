using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for retrieving provider model information from the API
    /// </summary>
    public class ProviderModelsService
    {
        private readonly IConduitApiClient _conduitApiClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ProviderModelsService> _logger;

        // Cache models for 30 minutes in the UI layer
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelsService"/> class.
        /// </summary>
        /// <param name="conduitApiClient">Conduit API client for making authenticated requests.</param>
        /// <param name="memoryCache">Memory cache for storing model lists.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ProviderModelsService(
            IConduitApiClient conduitApiClient,
            IMemoryCache memoryCache,
            ILogger<ProviderModelsService> logger)
        {
            _conduitApiClient = conduitApiClient ?? throw new ArgumentNullException(nameof(conduitApiClient));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets models for a specific provider with optional cache refresh
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <param name="forceRefresh">Whether to force a refresh of cached models</param>
        /// <returns>List of available model IDs</returns>
        public async Task<List<string>> GetModelsAsync(string providerName, bool forceRefresh = false)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            string cacheKey = $"provider_models_{providerName}";

            // Check cache first unless force refresh is requested
            if (!forceRefresh && _memoryCache.TryGetValue(cacheKey, out List<string>? cachedModels) && cachedModels != null)
            {
                _logger.LogDebug("Returning cached models for provider {ProviderName}", providerName);
                return cachedModels;
            }

            try
            {
                _logger.LogInformation("Fetching models for provider {ProviderName} from API", providerName);

                // Use the authenticated ConduitApiClient
                var models = await _conduitApiClient.GetProviderModelsAsync(providerName, forceRefresh);

                _logger.LogInformation("Retrieved {Count} models for provider {ProviderName}",
                    models.Count, providerName);

                // Cache the results
                _memoryCache.Set(cacheKey, models, _cacheDuration);

                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling provider models API for {ProviderName}", providerName);
                return new List<string>();
            }
        }
    }
}
