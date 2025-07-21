using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing provider models and model discovery through the Admin API.
/// </summary>
public class ProviderModelsService : BaseApiClient
{
    private const string BaseEndpoint = "/api/provider-models";
    private const string DiscoveryEndpoint = "/v1/discovery";
    private const int DefaultPageSize = 25;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ModelsCacheTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the ProviderModelsService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public ProviderModelsService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<ProviderModelsService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    /// <summary>
    /// Retrieves models available from a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of models available from the provider.</returns>
    /// <exception cref="ValidationException">Thrown when the provider name is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderModelDto>> GetProviderModelsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ValidationException("Provider name is required", "providerName");

            var cacheKey = GetCacheKey("provider-models", providerName);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/{Uri.EscapeDataString(providerName)}";
                var response = await GetAsync<IEnumerable<ProviderModelDto>>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved {Count} models from provider {Provider}", response.Count(), providerName);
                return response;
            }, ModelsCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all discovered models with capability information.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A response containing discovered models.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveredModelsResponse> GetDiscoveredModelsAsync(
        ProviderModelsFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("discovered-models", filters?.GetHashCode() ?? 0);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{DiscoveryEndpoint}/models";
                var response = await GetAsync<DiscoveredModelsResponse>(endpoint, filters, cancellationToken);
                
                _logger?.LogDebug("Retrieved {Count} discovered models", response.Data?.Count() ?? 0);
                return response;
            }, DefaultCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Retrieves models from a specific provider with capability information.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered models from the provider.</returns>
    /// <exception cref="ValidationException">Thrown when the provider name is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveredModelsResponse> GetProviderDiscoveredModelsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ValidationException("Provider name is required", "providerName");

            var cacheKey = GetCacheKey("provider-discovered-models", providerName);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{DiscoveryEndpoint}/providers/{Uri.EscapeDataString(providerName)}/models";
                var response = await GetAsync<DiscoveredModelsResponse>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved {Count} discovered models from provider {Provider}", 
                    response.Data?.Count() ?? 0, providerName);
                return response;
            }, DefaultCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Performs bulk model discovery across multiple providers.
    /// </summary>
    /// <param name="request">The bulk discovery request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered models from all requested providers.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveredModelsResponse> PerformBulkModelDiscoveryAsync(
        BulkModelDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateBulkDiscoveryRequest(request);
            
            _logger?.LogDebug("Performing bulk model discovery for {ModelCount} models", 
                request.Models?.Count() ?? 0);

            var endpoint = $"{DiscoveryEndpoint}/bulk/models";
            var response = await PostAsync<DiscoveredModelsResponse>(endpoint, request, cancellationToken);

            // Always invalidate cache for bulk discovery since it may have new data
            InvalidateModelCaches();

            _logger?.LogDebug("Bulk discovery completed - Found {Count} models", response.Data?.Count() ?? 0);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Performs bulk capability testing for specified models.
    /// </summary>
    /// <param name="request">The bulk capability test request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Capability test results for all requested models.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<BulkCapabilityTestResult> PerformBulkCapabilityTestAsync(
        BulkCapabilityTestRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateBulkCapabilityRequest(request);
            
            _logger?.LogDebug("Performing bulk capability testing for {TestCount} tests", 
                request.Tests.Count());

            var endpoint = $"{DiscoveryEndpoint}/bulk/capabilities";
            var response = await PostAsync<BulkCapabilityTestResult>(endpoint, request, cancellationToken);

            _logger?.LogDebug("Bulk capability testing completed in {Duration} - Tested {Count} models", 
                response.Duration, response.Results?.Count ?? 0);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets statistics about models from all providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics for models grouped by provider.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderModelStatsDto>> GetProviderModelStatsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            const string cacheKey = "provider-model-stats";
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/stats";
                var response = await GetAsync<IEnumerable<ProviderModelStatsDto>>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved model statistics for {ProviderCount} providers", response.Count());
                return response;
            }, ShortCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Searches for models across all providers by name or capability.
    /// </summary>
    /// <param name="searchTerm">The search term (model name or capability).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Models matching the search criteria.</returns>
    /// <exception cref="ValidationException">Thrown when the search term is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveredModelsResponse> SearchModelsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ValidationException("Search term is required", "searchTerm");

            var parameters = new { q = searchTerm };
            var cacheKey = GetCacheKey("search-models", searchTerm);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{DiscoveryEndpoint}/models/search";
                var response = await GetAsync<DiscoveredModelsResponse>(endpoint, parameters, cancellationToken);
                
                _logger?.LogDebug("Search for '{SearchTerm}' returned {Count} models", 
                    searchTerm, response.Data?.Count() ?? 0);
                return response;
            }, ShortCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets models that support a specific capability.
    /// </summary>
    /// <param name="capability">The capability to filter by (e.g., "chat", "vision", "embeddings").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Models that support the specified capability.</returns>
    /// <exception cref="ValidationException">Thrown when the capability is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveredModelsResponse> GetModelsByCapabilityAsync(
        string capability,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(capability))
                throw new ValidationException("Capability is required", "capability");

            var parameters = new { capability };
            var cacheKey = GetCacheKey("models-by-capability", capability);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{DiscoveryEndpoint}/models/capability";
                var response = await GetAsync<DiscoveredModelsResponse>(endpoint, parameters, cancellationToken);
                
                _logger?.LogDebug("Found {Count} models with capability '{Capability}'", 
                    response.Data?.Count() ?? 0, capability);
                return response;
            }, DefaultCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Tests the capabilities of a specific model.
    /// </summary>
    /// <param name="modelId">The model ID to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capability test result for the model.</returns>
    /// <exception cref="ValidationException">Thrown when the model ID is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelCapabilityTestResult> TestModelCapabilitiesAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ValidationException("Model ID is required", "modelId");

            _logger?.LogDebug("Testing capabilities for model {ModelId}", modelId);

            var endpoint = $"{DiscoveryEndpoint}/models/{Uri.EscapeDataString(modelId)}/test";
            var response = await PostAsync<ModelCapabilityTestResult>(endpoint, null, cancellationToken);

            _logger?.LogDebug("Capability test completed for model {ModelId} - Available: {Available}, Duration: {Duration}", 
                modelId, response.IsAvailable, response.Duration);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Forces a refresh of model data from all providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated model discovery response.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveredModelsResponse> RefreshAllModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Refreshing model data from all providers");

            // Get all models with capability information
            var response = await GetDiscoveredModelsAsync(null, cancellationToken);

            // Invalidate all model-related caches
            InvalidateModelCaches();

            _logger?.LogDebug("Model refresh completed - Found {Count} models", response.Data?.Count() ?? 0);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    private static void ValidateBulkDiscoveryRequest(BulkModelDiscoveryRequest request)
    {
        if (request == null)
            throw new ValidationException("Bulk discovery request is required", "request");

        if (!request.Models.Any())
            throw new ValidationException("At least one model must be provided", "Models");
    }

    private static void ValidateBulkCapabilityRequest(BulkCapabilityTestRequest request)
    {
        if (request == null)
            throw new ValidationException("Bulk capability request is required", "request");

        if (!request.Tests.Any())
            throw new ValidationException("At least one capability test must be provided", "Tests");
    }

    private void InvalidateModelCaches()
    {
        // Invalidate common cache patterns
        var prefixes = new[] 
        { 
            "provider-models", 
            "discovered-models", 
            "provider-discovered-models",
            "provider-model-stats",
            "search-models",
            "models-by-capability"
        };
        
        foreach (var prefix in prefixes)
        {
            // This is a simplified cache invalidation
            InvalidateCache(GetCacheKey(prefix));
        }

        _logger?.LogDebug("Invalidated model cache entries");
    }
}