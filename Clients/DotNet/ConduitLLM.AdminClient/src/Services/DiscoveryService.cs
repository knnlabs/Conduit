using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for model discovery and capability testing through the Admin API.
/// </summary>
public class DiscoveryService : BaseApiClient
{
    private const string BaseEndpoint = "/discovery";
    private const string ModelsEndpoint = "/discovery/models";
    private const string CapabilitiesEndpoint = "/discovery/capabilities";
    private const string BulkEndpoint = "/discovery/bulk";
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the DiscoveryService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public DiscoveryService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<DiscoveryService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Model Discovery

    /// <summary>
    /// Discovers all available models across providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A response containing all discovered models.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveryModelsResponse> DiscoverAllModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("discovery-all-models");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<DiscoveryModelsResponse>(ModelsEndpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ModelsEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Discovers models for a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name to discover models for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A response containing models for the specified provider.</returns>
    /// <exception cref="ValidationException">Thrown when the provider name is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveryProviderModelsResponse> DiscoverProviderModelsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ValidationException("Provider name is required");

            var endpoint = $"{ModelsEndpoint}/{Uri.EscapeDataString(providerName)}";
            var cacheKey = GetCacheKey("discovery-provider-models", providerName);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<DiscoveryProviderModelsResponse>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{ModelsEndpoint}/{providerName}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Performs bulk model discovery for multiple models.
    /// </summary>
    /// <param name="request">The bulk discovery request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery results for all requested models.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<BulkModelDiscoveryResponse> BulkDiscoverModelsAsync(
        BulkModelDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateBulkModelDiscoveryRequest(request);

            var endpoint = $"{BulkEndpoint}/models";
            return await PostAsync<BulkModelDiscoveryResponse>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BulkEndpoint}/models", "POST");
            throw;
        }
    }

    /// <summary>
    /// Discovers models with specific requirements.
    /// </summary>
    /// <param name="request">The model discovery request with requirements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery results for models matching the requirements.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<BulkModelDiscoveryResponse> DiscoverModelsWithRequirementsAsync(
        ModelDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateModelDiscoveryRequest(request);

            var endpoint = $"{ModelsEndpoint}/search";
            return await PostAsync<BulkModelDiscoveryResponse>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{ModelsEndpoint}/search", "POST");
            throw;
        }
    }

    #endregion

    #region Capability Testing

    /// <summary>
    /// Tests a specific capability for a model.
    /// </summary>
    /// <param name="model">The model to test.</param>
    /// <param name="capability">The capability to test.</param>
    /// <param name="virtualKey">Optional virtual key for authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capability test result.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CapabilityTestResponse> TestCapabilityAsync(
        string model,
        string capability,
        string? virtualKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new ValidationException("Model is required");

            if (string.IsNullOrWhiteSpace(capability))
                throw new ValidationException("Capability is required");

            var request = new CapabilityTest
            {
                Model = model,
                Capability = capability
            };

            var parameters = virtualKey != null ? new { virtualKey } : null;
            var endpoint = $"{CapabilitiesEndpoint}/test";

            return await PostAsync<CapabilityTestResponse>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CapabilitiesEndpoint}/test", "POST");
            throw;
        }
    }

    /// <summary>
    /// Performs bulk capability testing for multiple model-capability combinations.
    /// </summary>
    /// <param name="request">The bulk capability test request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Test results for all requested capability tests.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<BulkCapabilityTestResponse> BulkTestCapabilitiesAsync(
        BulkCapabilityTestRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateBulkCapabilityTestRequest(request);

            var endpoint = $"{BulkEndpoint}/capabilities";
            return await PostAsync<BulkCapabilityTestResponse>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BulkEndpoint}/capabilities", "POST");
            throw;
        }
    }

    /// <summary>
    /// Gets all available capabilities that can be tested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available capabilities.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<string>> GetAvailableCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("available-capabilities");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<string>>(CapabilitiesEndpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, CapabilitiesEndpoint, "GET");
            throw;
        }
    }

    #endregion

    #region Discovery Statistics

    /// <summary>
    /// Retrieves discovery system statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery system statistics.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DiscoveryStats> GetDiscoveryStatsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/stats";
            var cacheKey = GetCacheKey("discovery-stats");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<DiscoveryStats>(endpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/stats", "GET");
            throw;
        }
    }

    /// <summary>
    /// Clears the discovery cache to force fresh discovery.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task ClearDiscoveryCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/cache/clear";
            await PostAsync(endpoint, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/cache/clear", "POST");
            throw;
        }
    }

    #endregion

    #region Filtering and Search

    /// <summary>
    /// Finds models that support specific capabilities.
    /// </summary>
    /// <param name="capabilities">The capabilities to search for.</param>
    /// <param name="requireAll">Whether all capabilities must be supported (default: true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Models that support the specified capabilities.</returns>
    /// <exception cref="ValidationException">Thrown when no capabilities are provided.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<DiscoveryModel>> FindModelsByCapabilitiesAsync(
        IEnumerable<string> capabilities,
        bool requireAll = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilityList = capabilities?.ToList() ?? new List<string>();
            if (!capabilityList.Any())
                throw new ValidationException("At least one capability must be specified");

            var parameters = new
            {
                capabilities = string.Join(",", capabilityList),
                requireAll
            };

            var endpoint = $"{ModelsEndpoint}/by-capabilities";
            var cacheKey = GetCacheKey("models-by-capabilities", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<DiscoveryModel>>(endpoint, parameters, cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{ModelsEndpoint}/by-capabilities", "GET");
            throw;
        }
    }

    /// <summary>
    /// Searches for models by name pattern.
    /// </summary>
    /// <param name="pattern">The search pattern.</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Models matching the search pattern.</returns>
    /// <exception cref="ValidationException">Thrown when the pattern is empty.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<DiscoveryModel>> SearchModelsAsync(
        string pattern,
        bool caseSensitive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ValidationException("Search pattern is required");

            var parameters = new
            {
                pattern,
                caseSensitive
            };

            var endpoint = $"{ModelsEndpoint}/search";
            var cacheKey = GetCacheKey("models-search", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<DiscoveryModel>>(endpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{ModelsEndpoint}/search", "GET");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the standard discovery capabilities.
    /// </summary>
    /// <returns>A list of standard capabilities.</returns>
    public static IEnumerable<string> GetStandardCapabilities()
    {
        return new[]
        {
            DiscoveryCapabilities.Chat,
            DiscoveryCapabilities.ImageGeneration,
            DiscoveryCapabilities.Vision,
            DiscoveryCapabilities.AudioTranscription,
            DiscoveryCapabilities.TextToSpeech,
            DiscoveryCapabilities.Embeddings,
            DiscoveryCapabilities.CodeGeneration,
            DiscoveryCapabilities.FunctionCalling,
            DiscoveryCapabilities.Streaming
        };
    }

    private static void ValidateBulkModelDiscoveryRequest(BulkModelDiscoveryRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Models == null || !request.Models.Any())
            throw new ValidationException("At least one model must be specified");

        var modelList = request.Models.ToList();
        if (modelList.Count > 100)
            throw new ValidationException("Cannot discover more than 100 models in a single request");

        foreach (var model in modelList)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new ValidationException("Model names cannot be empty");
        }
    }

    private static void ValidateModelDiscoveryRequest(ModelDiscoveryRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Models == null || !request.Models.Any())
            throw new ValidationException("At least one model must be specified");

        var modelList = request.Models.ToList();
        if (modelList.Count > 100)
            throw new ValidationException("Cannot discover more than 100 models in a single request");
    }

    private static void ValidateBulkCapabilityTestRequest(BulkCapabilityTestRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Tests == null || !request.Tests.Any())
            throw new ValidationException("At least one capability test must be specified");

        var testList = request.Tests.ToList();
        if (testList.Count > 50)
            throw new ValidationException("Cannot test more than 50 capabilities in a single request");

        foreach (var test in testList)
        {
            if (string.IsNullOrWhiteSpace(test.Model))
                throw new ValidationException("Model is required for each test");

            if (string.IsNullOrWhiteSpace(test.Capability))
                throw new ValidationException("Capability is required for each test");
        }
    }

    #endregion
}