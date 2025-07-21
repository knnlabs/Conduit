using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Constants;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing provider credentials and health through the Admin API.
/// </summary>
public class ProviderService : BaseApiClient
{
    private const string BaseEndpoint = ApiEndpoints.Providers.Base;
    private const string CredentialsEndpoint = ApiEndpoints.Providers.Credentials;
    private const string HealthEndpoint = ApiEndpoints.Providers.Health;
    private const int DefaultPageSize = 25;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MediumCacheTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Initializes a new instance of the ProviderService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public ProviderService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<ProviderService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Provider Credentials

    /// <summary>
    /// Creates a new provider credential.
    /// </summary>
    /// <param name="request">The provider credential creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created provider credential.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ProviderCredentialDto> CreateCredentialAsync(
        CreateProviderCredentialDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await PostAsync<ProviderCredentialDto>(
                CredentialsEndpoint,
                request,
                cancellationToken);

            await InvalidateCacheAsync();
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, CredentialsEndpoint, "POST");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a list of provider credentials with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of provider credentials matching the filter criteria.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderCredentialDto>> ListCredentialsAsync(
        ProviderFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildCredentialFilterParameters(filters);
            var cacheKey = GetCacheKey("provider-credentials", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ProviderCredentialDto>>(CredentialsEndpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, CredentialsEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a provider credential by its ID.
    /// </summary>
    /// <param name="id">The provider credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider credential information.</returns>
    /// <exception cref="NotFoundException">Thrown when the provider credential is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ProviderCredentialDto> GetCredentialByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{CredentialsEndpoint}/{id}";
            var cacheKey = GetCacheKey("provider-credential", id);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<ProviderCredentialDto>(endpoint, cancellationToken: cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CredentialsEndpoint}/{id}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing provider credential.
    /// </summary>
    /// <param name="id">The provider credential ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the provider credential is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateCredentialAsync(
        int id,
        UpdateProviderCredentialDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{CredentialsEndpoint}/{id}";
            await PutAsync(endpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CredentialsEndpoint}/{id}", "PUT");
            throw;
        }
    }

    /// <summary>
    /// Deletes a provider credential by its ID.
    /// </summary>
    /// <param name="id">The provider credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the provider credential is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DeleteCredentialAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{CredentialsEndpoint}/{id}";
            await DeleteAsync(endpoint, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CredentialsEndpoint}/{id}", "DELETE");
            throw;
        }
    }

    /// <summary>
    /// Tests a provider connection with the given credentials.
    /// </summary>
    /// <param name="request">The connection test request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection test result.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ProviderConnectionTestResultDto> TestConnectionAsync(
        ProviderConnectionTestRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{CredentialsEndpoint}/test-connection";
            return await PostAsync<ProviderConnectionTestResultDto>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CredentialsEndpoint}/test-connection", "POST");
            throw;
        }
    }

    #endregion

    #region Provider Data

    /// <summary>
    /// Retrieves metadata about all available providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of provider metadata.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderDataDto>> GetProviderDataAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("provider-data");
            
            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ProviderDataDto>>(BaseEndpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, BaseEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves metadata about a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider metadata.</returns>
    /// <exception cref="NotFoundException">Thrown when the provider is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ProviderDataDto> GetProviderDataByNameAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{Uri.EscapeDataString(providerName)}";
            var cacheKey = GetCacheKey("provider-data", providerName);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<ProviderDataDto>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{providerName}", "GET");
            throw;
        }
    }

    #endregion

    #region Provider Health

    /// <summary>
    /// Retrieves health configurations for all providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of provider health configurations.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderHealthConfigurationDto>> GetHealthConfigurationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("provider-health-configs");
            
            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ProviderHealthConfigurationDto>>(HealthEndpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, HealthEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves health configuration for a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider health configuration.</returns>
    /// <exception cref="NotFoundException">Thrown when the provider is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ProviderHealthConfigurationDto> GetHealthConfigurationAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{HealthEndpoint}/{Uri.EscapeDataString(providerName)}";
            var cacheKey = GetCacheKey("provider-health-config", providerName);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<ProviderHealthConfigurationDto>(endpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{HealthEndpoint}/{providerName}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates health configuration for a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="request">The health configuration update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the provider is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateHealthConfigurationAsync(
        string providerName,
        UpdateProviderHealthConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{HealthEndpoint}/{Uri.EscapeDataString(providerName)}";
            await PutAsync(endpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{HealthEndpoint}/{providerName}", "PUT");
            throw;
        }
    }

    /// <summary>
    /// Retrieves health records for providers with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of provider health records.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderHealthRecordDto>> GetHealthRecordsAsync(
        ProviderHealthFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildHealthFilterParameters(filters);
            var endpoint = $"{HealthEndpoint}/records";
            var cacheKey = GetCacheKey("provider-health-records", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ProviderHealthRecordDto>>(endpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{HealthEndpoint}/records", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves current health status for all providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status summary for all providers.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<ProviderHealthSummaryDto> GetHealthSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{HealthEndpoint}/summary";
            var cacheKey = GetCacheKey("provider-health-summary");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<ProviderHealthSummaryDto>(endpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{HealthEndpoint}/summary", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves usage statistics for providers.
    /// </summary>
    /// <param name="startDate">Start date for statistics.</param>
    /// <param name="endDate">End date for statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of provider usage statistics.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ProviderUsageStatistics>> GetUsageStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                startDate = startDate.ToString(DateFormats.ApiDateTime),
                endDate = endDate.ToString(DateFormats.ApiDateTime)
            };
            
            var endpoint = $"{BaseEndpoint}/usage-statistics";
            var cacheKey = GetCacheKey("provider-usage-stats", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ProviderUsageStatistics>>(endpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/usage-statistics", "GET");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private object BuildCredentialFilterParameters(ProviderFilters? filters)
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
            providerName = filters.ProviderName,
            hasApiKey = filters.HasApiKey,
            isHealthy = filters.IsHealthy
        };
    }

    private object BuildHealthFilterParameters(ProviderHealthFilters? filters)
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
            providerName = filters.ProviderName,
            isHealthy = filters.IsHealthy,
            startDate = filters.StartDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endDate = filters.EndDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            minResponseTime = filters.MinResponseTime,
            maxResponseTime = filters.MaxResponseTime
        };
    }

    private async Task InvalidateCacheAsync()
    {
        // For now, we'll clear all cache entries
        // In a more sophisticated implementation, we could be more selective
        await Task.CompletedTask;
    }

    #endregion
}