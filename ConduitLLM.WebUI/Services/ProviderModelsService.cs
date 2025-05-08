using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for retrieving provider model information from the API
    /// </summary>
    public class ProviderModelsService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ProviderModelsService> _logger;
        
        // Cache models for 30 minutes in the UI layer
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelsService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
        /// <param name="memoryCache">Memory cache for storing model lists.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ProviderModelsService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            ILogger<ProviderModelsService> logger)
        {
            _httpClient = httpClientFactory?.CreateClient("ConduitAPI") 
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
                
                // Call the API
                var response = await _httpClient.GetAsync(
                    $"api/provider-models/{providerName}?forceRefresh={forceRefresh}");
                
                if (response.IsSuccessStatusCode)
                {
                    var models = await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    
                    _logger.LogInformation("Retrieved {Count} models for provider {ProviderName}", 
                        models.Count, providerName);
                    
                    // Cache the results
                    _memoryCache.Set(cacheKey, models, _cacheDuration);
                    
                    return models;
                }
                
                // Handle error responses
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Error retrieving models for provider {ProviderName}: {StatusCode} - {ErrorContent}", 
                    providerName, response.StatusCode, errorContent);
                    
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling provider models API for {ProviderName}", providerName);
                return new List<string>();
            }
        }
    }
}