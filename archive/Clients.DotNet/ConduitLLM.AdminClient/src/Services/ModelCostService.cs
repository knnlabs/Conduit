using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing model costs through the Admin API.
/// </summary>
public class ModelCostService : BaseApiClient
{
    private const string BaseEndpoint = "/api/ModelCosts";
    private const int DefaultPageSize = 25;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Initializes a new instance of the ModelCostService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public ModelCostService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<ModelCostService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    /// <summary>
    /// Retrieves all model costs with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of model costs.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<PaginatedResponse<ModelCostDto>> ListAsync(
        ModelCostFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("model-costs", filters?.GetHashCode() ?? 0);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var response = await GetAsync<PaginatedResponse<ModelCostDto>>(
                    BaseEndpoint,
                    filters,
                    cancellationToken);

                _logger?.LogDebug("Retrieved {Count} model costs", response.Items?.Count() ?? 0);
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
    /// Retrieves a specific model cost by ID.
    /// </summary>
    /// <param name="id">The model cost ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model cost information.</returns>
    /// <exception cref="ValidationException">Thrown when the ID is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the model cost is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelCostDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                throw new ValidationException("Model cost ID must be greater than zero", "id");

            var cacheKey = GetCacheKey("model-cost", id);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/{id}";
                var response = await GetAsync<ModelCostDto>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved model cost {Id} for pattern {Pattern}", response.Id, response.ModelIdPattern);
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
    /// Retrieves model costs for a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of model costs for the provider.</returns>
    /// <exception cref="ValidationException">Thrown when the provider name is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ModelCostDto>> GetByProviderAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ValidationException("Provider name is required", "providerName");

            var cacheKey = GetCacheKey("model-costs-provider", providerName);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/provider/{Uri.EscapeDataString(providerName)}";
                var response = await GetAsync<IEnumerable<ModelCostDto>>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved {Count} model costs for provider {Provider}", response.Count(), providerName);
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
    /// Retrieves a model cost by pattern.
    /// </summary>
    /// <param name="pattern">The model ID pattern.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model cost information.</returns>
    /// <exception cref="ValidationException">Thrown when the pattern is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when no matching cost is found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelCostDto> GetByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ValidationException("Model pattern is required", "pattern");

            var cacheKey = GetCacheKey("model-cost-pattern", pattern);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/pattern/{Uri.EscapeDataString(pattern)}";
                var response = await GetAsync<ModelCostDto>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved model cost for pattern {Pattern}", pattern);
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
    /// Creates a new model cost configuration.
    /// </summary>
    /// <param name="request">The model cost creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created model cost.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelCostDto> CreateAsync(
        CreateModelCostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateCreateRequest(request);
            
            _logger?.LogDebug("Creating model cost for pattern {Pattern}", request.ModelIdPattern);

            var response = await PostAsync<ModelCostDto>(
                BaseEndpoint,
                request,
                cancellationToken);

            // Invalidate related cache entries
            InvalidateModelCostCaches();

            _logger?.LogDebug("Created model cost {Id} for pattern {Pattern}", response.Id, response.ModelIdPattern);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing model cost configuration.
    /// </summary>
    /// <param name="id">The model cost ID to update.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated model cost.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the model cost is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ModelCostDto> UpdateAsync(
        int id,
        UpdateModelCostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                throw new ValidationException("Model cost ID must be greater than zero", "id");

            ValidateUpdateRequest(request);
            
            _logger?.LogDebug("Updating model cost {Id}", id);

            var endpoint = $"{BaseEndpoint}/{id}";
            var response = await PutAsync<ModelCostDto>(endpoint, request, cancellationToken);

            // Invalidate related cache entries
            InvalidateModelCostCaches();
            InvalidateCache(GetCacheKey("model-cost", id));

            _logger?.LogDebug("Updated model cost {Id}", response.Id);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Deletes a model cost configuration.
    /// </summary>
    /// <param name="id">The model cost ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the ID is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the model cost is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                throw new ValidationException("Model cost ID must be greater than zero", "id");

            _logger?.LogDebug("Deleting model cost {Id}", id);

            var endpoint = $"{BaseEndpoint}/{id}";
            await DeleteAsync(endpoint, cancellationToken);

            // Invalidate related cache entries
            InvalidateModelCostCaches();
            InvalidateCache(GetCacheKey("model-cost", id));

            _logger?.LogDebug("Deleted model cost {Id}", id);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets cost overview for a time period.
    /// </summary>
    /// <param name="startDate">Start date for the overview.</param>
    /// <param name="endDate">End date for the overview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cost overview information.</returns>
    /// <exception cref="ValidationException">Thrown when the date range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CostOverviewDto> GetOverviewAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (startDate >= endDate)
                throw new ValidationException("Start date must be before end date", "dateRange");

            var parameters = new
            {
                startDate = startDate.ToString("O"),
                endDate = endDate.ToString("O")
            };

            var cacheKey = GetCacheKey("cost-overview", startDate.Date, endDate.Date);
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/overview";
                var response = await GetAsync<CostOverviewDto>(endpoint, parameters, cancellationToken);
                
                _logger?.LogDebug("Retrieved cost overview for period {StartDate} to {EndDate}", 
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
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
    /// Imports model costs in bulk.
    /// </summary>
    /// <param name="request">The import request containing model costs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import result.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ImportModelCostsResult> ImportAsync(
        ImportModelCostsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateImportRequest(request);
            
            _logger?.LogDebug("Importing {Count} model costs", request.ModelCosts.Count());

            var endpoint = $"{BaseEndpoint}/import";
            var response = await PostAsync<ImportModelCostsResult>(endpoint, request, cancellationToken);

            // Invalidate all cost-related cache entries
            InvalidateModelCostCaches();

            _logger?.LogDebug("Imported {Imported} model costs, updated {Updated}, skipped {Skipped}", 
                response.Imported, response.Updated, response.Skipped);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    private static void ValidateCreateRequest(CreateModelCostRequest request)
    {
        if (request == null)
            throw new ValidationException("Create request is required", "request");

        if (string.IsNullOrWhiteSpace(request.ModelIdPattern))
            throw new ValidationException("Model ID pattern is required", "ModelIdPattern");

        if (request.InputTokenCost < 0)
            throw new ValidationException("Input token cost cannot be negative", "InputTokenCost");

        if (request.OutputTokenCost < 0)
            throw new ValidationException("Output token cost cannot be negative", "OutputTokenCost");
    }

    private static void ValidateUpdateRequest(UpdateModelCostRequest request)
    {
        if (request == null)
            throw new ValidationException("Update request is required", "request");

        // At least one field should be provided for update
        if (request.ModelIdPattern == null && request.InputTokenCost == null && 
            request.OutputTokenCost == null && request.EmbeddingTokenCost == null &&
            request.ImageCostPerImage == null && request.AudioCostPerMinute == null &&
            request.AudioCostPerKCharacters == null && request.AudioInputCostPerMinute == null &&
            request.AudioOutputCostPerMinute == null && request.Description == null &&
            request.Priority == null)
        {
            throw new ValidationException("At least one field must be provided for update", "request");
        }

        if (request.InputTokenCost.HasValue && request.InputTokenCost.Value < 0)
            throw new ValidationException("Input token cost cannot be negative", "InputTokenCost");

        if (request.OutputTokenCost.HasValue && request.OutputTokenCost.Value < 0)
            throw new ValidationException("Output token cost cannot be negative", "OutputTokenCost");
    }

    private static void ValidateImportRequest(ImportModelCostsRequest request)
    {
        if (request == null)
            throw new ValidationException("Import request is required", "request");

        if (!request.ModelCosts.Any())
            throw new ValidationException("At least one model cost must be provided for import", "ModelCosts");

        foreach (var cost in request.ModelCosts)
        {
            ValidateCreateRequest(cost);
        }
    }

    private void InvalidateModelCostCaches()
    {
        // Invalidate common cache patterns
        var prefixes = new[] { "model-costs", "model-cost", "cost-overview" };
        
        foreach (var prefix in prefixes)
        {
            // This is a simplified cache invalidation - in a real implementation,
            // you might want to track cache keys or use cache tags
            InvalidateCache(GetCacheKey(prefix));
        }
    }
}