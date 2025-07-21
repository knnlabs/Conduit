using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Constants;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing virtual keys through the Admin API.
/// </summary>
public class VirtualKeyService : BaseApiClient
{
    private const string BaseEndpoint = ApiEndpoints.VirtualKeys.Base;
    private const int DefaultPageSize = 25;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MediumCacheTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Initializes a new instance of the VirtualKeyService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public VirtualKeyService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<VirtualKeyService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    /// <summary>
    /// Creates a new virtual key.
    /// </summary>
    /// <param name="request">The virtual key creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created virtual key information including the key value.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CreateVirtualKeyResponse> CreateAsync(
        CreateVirtualKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await PostAsync<CreateVirtualKeyResponse>(
                BaseEndpoint,
                request,
                cancellationToken);

            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, BaseEndpoint, "POST");
            throw; // This line will never be reached due to HandleException always throwing
        }
    }

    /// <summary>
    /// Retrieves a list of virtual keys with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of virtual keys matching the filter criteria.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<VirtualKeyDto>> ListAsync(
        VirtualKeyFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildFilterParameters(filters);
            var cacheKey = GetCacheKey("virtual-keys", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<VirtualKeyDto>>(BaseEndpoint, parameters, cancellationToken),
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
    /// Retrieves a virtual key by its ID.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The virtual key information.</returns>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<VirtualKeyDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}";
            var cacheKey = GetCacheKey("virtual-key", id);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<VirtualKeyDto>(endpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing virtual key.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateAsync(
        int id,
        UpdateVirtualKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
    /// Deletes a virtual key by its ID.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
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

    /// <summary>
    /// Searches for virtual keys by query string.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of virtual keys matching the search query.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<VirtualKeyDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var filters = new VirtualKeyFilters
        {
            Search = query,
            PageSize = 100
        };
        
        return await ListAsync(filters, cancellationToken);
    }

    /// <summary>
    /// Resets the spend amount for a virtual key.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task ResetSpendAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}/reset-spend";
            await PostAsync(endpoint, cancellationToken: cancellationToken);
            InvalidateCache(GetCacheKey("virtual-key", id));
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}/reset-spend", "POST");
            throw;
        }
    }

    /// <summary>
    /// Validates a virtual key.
    /// </summary>
    /// <param name="key">The virtual key to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ValidationException">Thrown when the key format is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<VirtualKeyValidationResult> ValidateAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new VirtualKeyValidationRequest { Key = key };
            var endpoint = $"{BaseEndpoint}/validate";
            
            return await PostAsync<VirtualKeyValidationResult>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/validate", "POST");
            throw;
        }
    }

    /// <summary>
    /// Updates the spend amount for a virtual key.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="request">The spend update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateSpendAsync(
        int id,
        UpdateSpendRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}/spend";
            await PostAsync(endpoint, request, cancellationToken);
            InvalidateCache(GetCacheKey("virtual-key", id));
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}/spend", "POST");
            throw;
        }
    }

    /// <summary>
    /// Refunds spend amount for a virtual key.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="request">The refund request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid or refund amount exceeds current spend.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task RefundSpendAsync(
        int id,
        RefundSpendRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}/refund";
            await PostAsync(endpoint, request, cancellationToken);
            InvalidateCache(GetCacheKey("virtual-key", id));
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}/refund", "POST");
            throw;
        }
    }

    /// <summary>
    /// Checks if a virtual key has sufficient budget for an estimated cost.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="estimatedCost">The estimated cost to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The budget check result.</returns>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CheckBudgetResponse> CheckBudgetAsync(
        int id,
        decimal estimatedCost,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CheckBudgetRequest { EstimatedCost = estimatedCost };
            var endpoint = $"{BaseEndpoint}/{id}/check-budget";
            
            return await PostAsync<CheckBudgetResponse>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}/check-budget", "POST");
            throw;
        }
    }

    /// <summary>
    /// Retrieves detailed validation information for a virtual key.
    /// </summary>
    /// <param name="id">The virtual key ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation information.</returns>
    /// <exception cref="NotFoundException">Thrown when the virtual key is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<VirtualKeyValidationInfo> GetValidationInfoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}/validation-info";
            return await GetAsync<VirtualKeyValidationInfo>(endpoint, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}/validation-info", "GET");
            throw;
        }
    }

    /// <summary>
    /// Performs maintenance operations on virtual keys.
    /// </summary>
    /// <param name="request">The maintenance request (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The maintenance operation results.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<VirtualKeyMaintenanceResponse> PerformMaintenanceAsync(
        VirtualKeyMaintenanceRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/maintenance";
            var response = await PostAsync<VirtualKeyMaintenanceResponse>(
                endpoint, 
                request ?? new VirtualKeyMaintenanceRequest(), 
                cancellationToken);
            
            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/maintenance", "POST");
            throw;
        }
    }

    /// <summary>
    /// Retrieves statistics about virtual keys.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Virtual key statistics.</returns>
    /// <exception cref="NotImplementedException">Thrown if the endpoint is not implemented in the Admin API.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<VirtualKeyStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "GetStatistics requires Admin API endpoint implementation. " +
            "The WebUI currently calculates statistics client-side by fetching all keys.");
    }

    /// <summary>
    /// Creates multiple virtual keys in a single request.
    /// </summary>
    /// <param name="requests">The virtual key creation requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created virtual key information for each request.</returns>
    /// <exception cref="NotImplementedException">Thrown if the endpoint is not implemented in the Admin API.</exception>
    public async Task<IEnumerable<CreateVirtualKeyResponse>> BulkCreateAsync(
        IEnumerable<CreateVirtualKeyRequest> requests,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "BulkCreate requires Admin API endpoint implementation. " +
            "Consider implementing POST /api/virtualkeys/bulk for batch creation.");
    }

    /// <summary>
    /// Exports virtual keys in the specified format.
    /// </summary>
    /// <param name="format">The export format (csv or json).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported data as a stream.</returns>
    /// <exception cref="NotImplementedException">Thrown if the endpoint is not implemented in the Admin API.</exception>
    public async Task<Stream> ExportKeysAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "ExportKeys requires Admin API endpoint implementation. " +
            "Consider implementing GET /api/virtualkeys/export with format parameter.");
    }

    private object BuildFilterParameters(VirtualKeyFilters? filters)
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
            isEnabled = filters.IsEnabled,
            hasExpired = filters.HasExpired,
            budgetDuration = filters.BudgetDuration?.ToString(),
            minBudget = filters.MinBudget,
            maxBudget = filters.MaxBudget,
            allowedModel = filters.AllowedModel,
            createdAfter = filters.CreatedAfter?.ToString(DateFormats.ApiDateTime),
            createdBefore = filters.CreatedBefore?.ToString(DateFormats.ApiDateTime),
            lastUsedAfter = filters.LastUsedAfter?.ToString(DateFormats.ApiDateTime),
            lastUsedBefore = filters.LastUsedBefore?.ToString(DateFormats.ApiDateTime)
        };
    }

    private async Task InvalidateCacheAsync()
    {
        // For now, we'll clear all cache entries
        // In a more sophisticated implementation, we could be more selective
        await Task.CompletedTask;
    }
}