using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing model provider mappings through the Admin API.
/// </summary>
public class ModelMappingService : BaseApiClient
{
    private const string BaseEndpoint = "/modelprovidermapping";
    private const string ProvidersEndpoint = "/modelprovidermapping/providers";
    private const string BulkEndpoint = "/modelprovidermapping/bulk";
    private const int DefaultPageSize = 25;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LongCacheTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of the ModelMappingService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public ModelMappingService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<ModelMappingService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region CRUD Operations

    /// <summary>
    /// Creates a new model provider mapping.
    /// </summary>
    /// <param name="request">The model mapping creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created model provider mapping.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelProviderMappingDto> CreateAsync(
        CreateModelProviderMappingDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateCreateRequest(request);

            var response = await PostAsync<ModelProviderMappingDto>(
                BaseEndpoint,
                request,
                cancellationToken);

            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, BaseEndpoint, "POST");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a list of model provider mappings with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of model provider mappings matching the filter criteria.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ModelProviderMappingDto>> ListAsync(
        ModelMappingFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildFilterParameters(filters);
            var cacheKey = GetCacheKey("model-mappings", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ModelProviderMappingDto>>(BaseEndpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, BaseEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a model provider mapping by its ID.
    /// </summary>
    /// <param name="id">The mapping ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model provider mapping information.</returns>
    /// <exception cref="NotFoundException">Thrown when the mapping is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelProviderMappingDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}";
            var cacheKey = GetCacheKey("model-mapping", id);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<ModelProviderMappingDto>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all mappings for a specific model.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of model provider mappings for the specified model.</returns>
    /// <exception cref="ValidationException">Thrown when the model ID is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ModelProviderMappingDto>> GetByModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ValidationException("Model ID is required");

            var endpoint = $"{BaseEndpoint}/model/{Uri.EscapeDataString(modelId)}";
            var cacheKey = GetCacheKey("model-mapping-by-model", modelId);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ModelProviderMappingDto>>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/model/{modelId}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing model provider mapping.
    /// </summary>
    /// <param name="id">The mapping ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the mapping is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateAsync(
        int id,
        UpdateModelProviderMappingDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateUpdateRequest(request);

            var endpoint = $"{BaseEndpoint}/{id}";
            await PutAsync(endpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}", "PUT");
            throw;
        }
    }

    /// <summary>
    /// Deletes a model provider mapping by its ID.
    /// </summary>
    /// <param name="id">The mapping ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the mapping is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}";
            await DeleteAsync(endpoint, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}", "DELETE");
            throw;
        }
    }

    #endregion

    #region Provider Operations

    /// <summary>
    /// Retrieves a list of available providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available provider names.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<string>> GetAvailableProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("available-providers");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<string>>(ProvidersEndpoint, cancellationToken: cancellationToken),
                LongCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, ProvidersEndpoint, "GET");
            throw;
        }
    }

    #endregion

    #region Management Operations

    /// <summary>
    /// Updates the priority of a model provider mapping.
    /// </summary>
    /// <param name="id">The mapping ID.</param>
    /// <param name="priority">The new priority value (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the priority is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the mapping is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdatePriorityAsync(
        int id,
        int priority,
        CancellationToken cancellationToken = default)
    {
        if (priority < 0 || priority > 100)
            throw new ValidationException("Priority must be between 0 and 100");

        await UpdateAsync(id, new UpdateModelProviderMappingDto { Priority = priority }, cancellationToken);
    }

    /// <summary>
    /// Enables a model provider mapping.
    /// </summary>
    /// <param name="id">The mapping ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the mapping is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task EnableAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await UpdateAsync(id, new UpdateModelProviderMappingDto { IsEnabled = true }, cancellationToken);
    }

    /// <summary>
    /// Disables a model provider mapping.
    /// </summary>
    /// <param name="id">The mapping ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the mapping is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DisableAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await UpdateAsync(id, new UpdateModelProviderMappingDto { IsEnabled = false }, cancellationToken);
    }

    /// <summary>
    /// Reorders model provider mappings by updating their priorities.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="mappingIds">The mapping IDs in the desired order (highest priority first).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task ReorderMappingsAsync(
        string modelId,
        IEnumerable<int> mappingIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ValidationException("Model ID is required");

        var idList = mappingIds?.ToList() ?? new List<int>();
        if (!idList.Any())
            throw new ValidationException("At least one mapping ID must be provided");

        var updateTasks = idList.Select((id, index) => 
            UpdatePriorityAsync(id, idList.Count - index, cancellationToken));

        await Task.WhenAll(updateTasks);
    }

    #endregion

    #region Routing Operations

    /// <summary>
    /// Retrieves routing information for a specific model.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Routing information for the specified model.</returns>
    /// <exception cref="ValidationException">Thrown when the model ID is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<ModelRoutingInfo> GetRoutingInfoAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ValidationException("Model ID is required");

            var endpoint = $"{BaseEndpoint}/routing/{Uri.EscapeDataString(modelId)}";
            var cacheKey = GetCacheKey("model-routing-info", modelId);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<ModelRoutingInfo>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/routing/{modelId}", "GET");
            throw;
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Creates multiple model provider mappings in a single operation.
    /// </summary>
    /// <param name="request">The bulk mapping creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the bulk creation operation.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<BulkMappingResponse> BulkCreateAsync(
        BulkMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateBulkRequest(request);

            var response = await PostAsync<BulkMappingResponse>(
                BulkEndpoint,
                request,
                cancellationToken);

            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, BulkEndpoint, "POST");
            throw;
        }
    }

    /// <summary>
    /// Imports model provider mappings from a file.
    /// </summary>
    /// <param name="fileStream">The file stream containing the mappings.</param>
    /// <param name="format">The file format (csv or json).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the import operation.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<BulkMappingResponse> ImportMappingsAsync(
        Stream fileStream,
        string format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (fileStream == null)
                throw new ValidationException("File stream is required");

            var supportedFormats = new[] { "csv", "json" };
            if (!supportedFormats.Contains(format?.ToLower()))
                throw new ValidationException($"Unsupported format: {format}. Supported formats: {string.Join(", ", supportedFormats)}");

            var endpoint = $"{BaseEndpoint}/import";
            
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", $"mappings.{format.ToLower()}");
            content.Add(new StringContent(format.ToLower()), "format");

            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response, endpoint, "POST");

            var result = await response.Content.ReadFromJsonAsync<BulkMappingResponse>(cancellationToken: cancellationToken);
            await InvalidateCacheAsync();
            
            return result ?? throw new ConduitAdminException("Invalid response from server", null, null, null, null);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/import", "POST");
            throw;
        }
    }

    /// <summary>
    /// Exports model provider mappings to a file.
    /// </summary>
    /// <param name="format">The export format (csv or json).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the exported data.</returns>
    /// <exception cref="ValidationException">Thrown when the format is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<Stream> ExportMappingsAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var supportedFormats = new[] { "csv", "json" };
            if (!supportedFormats.Contains(format?.ToLower()))
                throw new ValidationException($"Unsupported format: {format}. Supported formats: {string.Join(", ", supportedFormats)}");

            var endpoint = $"{BaseEndpoint}/export";
            var parameters = new { format = format.ToLower() };
            
            var response = await HttpClient.GetAsync($"{endpoint}?format={Uri.EscapeDataString(format.ToLower())}", cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response, endpoint, "GET");

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/export", "GET");
            throw;
        }
    }

    /// <summary>
    /// Suggests optimal provider mappings for a model.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Suggested optimal provider mappings.</returns>
    /// <exception cref="ValidationException">Thrown when the model ID is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<ModelMappingSuggestion> SuggestOptimalMappingAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ValidationException("Model ID is required");

            var endpoint = $"{BaseEndpoint}/suggest";
            var request = new { modelId };

            return await PostAsync<ModelMappingSuggestion>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/suggest", "POST");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static void ValidateCreateRequest(CreateModelProviderMappingDto request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.ModelId))
            throw new ValidationException("Model ID is required");

        if (string.IsNullOrWhiteSpace(request.ProviderId))
            throw new ValidationException("Provider ID is required");

        if (string.IsNullOrWhiteSpace(request.ProviderModelId))
            throw new ValidationException("Provider Model ID is required");

        if (request.Priority.HasValue && (request.Priority < 0 || request.Priority > 100))
            throw new ValidationException("Priority must be between 0 and 100");
    }

    private static void ValidateUpdateRequest(UpdateModelProviderMappingDto request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Priority.HasValue && (request.Priority < 0 || request.Priority > 100))
            throw new ValidationException("Priority must be between 0 and 100");
    }

    private static void ValidateBulkRequest(BulkMappingRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Mappings == null || !request.Mappings.Any())
            throw new ValidationException("At least one mapping must be provided");

        if (request.Mappings.Count() > 100)
            throw new ValidationException("Cannot create more than 100 mappings in a single request");

        foreach (var mapping in request.Mappings)
        {
            ValidateCreateRequest(mapping);
        }
    }

    private object BuildFilterParameters(ModelMappingFilters? filters)
    {
        if (filters == null)
        {
            return new { pageNumber = 1, pageSize = DefaultPageSize };
        }

        return new
        {
            pageNumber = filters.PageNumber ?? 1,
            pageSize = filters.PageSize ?? DefaultPageSize,
            search = filters.Search,
            sortBy = filters.SortBy?.Field,
            sortDirection = filters.SortBy?.Direction.ToString().ToLower(),
            modelId = filters.ModelId,
            providerId = filters.ProviderId,
            isEnabled = filters.IsEnabled,
            minPriority = filters.MinPriority,
            maxPriority = filters.MaxPriority,
            supportsVision = filters.SupportsVision,
            supportsImageGeneration = filters.SupportsImageGeneration,
            supportsAudioTranscription = filters.SupportsAudioTranscription,
            supportsTextToSpeech = filters.SupportsTextToSpeech,
            supportsRealtimeAudio = filters.SupportsRealtimeAudio,
            isDefault = filters.IsDefault,
            defaultCapabilityType = filters.DefaultCapabilityType
        };
    }

    private async Task InvalidateCacheAsync()
    {
        // Clear all model mapping related cache entries
        await Task.CompletedTask;
    }

    #endregion
}