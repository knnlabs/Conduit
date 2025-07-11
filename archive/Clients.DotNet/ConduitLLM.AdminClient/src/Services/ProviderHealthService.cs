using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing provider health monitoring and status.
/// </summary>
public class ProviderHealthService : BaseApiClient
{
    private const string BaseEndpoint = "/api/ProviderHealth";

    /// <summary>
    /// Initializes a new instance of the ProviderHealthService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public ProviderHealthService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<ProviderHealthService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Configuration Management

    /// <summary>
    /// Gets the health configuration for a specific provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider health configuration.</returns>
    public async Task<ProviderHealthConfigurationDto> GetProviderHealthConfigurationAsync(
        string providerName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        _logger?.LogDebug("Getting health configuration for provider {ProviderName}", providerName);

        try
        {
            var configuration = await GetAsync<ProviderHealthConfigurationDto>(
                $"{BaseEndpoint}/configuration/{Uri.EscapeDataString(providerName)}", 
                cancellationToken: cancellationToken);

            return configuration;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get health configuration for provider {ProviderName}", providerName);
            throw;
        }
    }

    /// <summary>
    /// Creates a new provider health configuration.
    /// </summary>
    /// <param name="request">The configuration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created configuration.</returns>
    public async Task<ProviderHealthConfigurationDto> CreateProviderHealthConfigurationAsync(
        CreateProviderHealthConfigurationDto request, 
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger?.LogDebug("Creating health configuration for provider {ProviderName}", request.ProviderName);

        try
        {
            var configuration = await PostAsync<ProviderHealthConfigurationDto>(
                $"{BaseEndpoint}/configuration", 
                request, 
                cancellationToken);

            _logger?.LogInformation("Created health configuration for provider {ProviderName}", request.ProviderName);
            return configuration;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create health configuration for provider {ProviderName}", request.ProviderName);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing provider health configuration.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated configuration.</returns>
    public async Task<ProviderHealthConfigurationDto> UpdateProviderHealthConfigurationAsync(
        string providerName,
        UpdateProviderHealthConfigurationDto request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger?.LogDebug("Updating health configuration for provider {ProviderName}", providerName);

        try
        {
            var result = await PutAsync<ProviderHealthConfigurationDto>(
                $"{BaseEndpoint}/configuration/{Uri.EscapeDataString(providerName)}", 
                request, 
                cancellationToken);

            _logger?.LogInformation("Updated health configuration for provider {ProviderName}", providerName);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update health configuration for provider {ProviderName}", providerName);
            throw;
        }
    }

    #endregion

    #region Health Records

    /// <summary>
    /// Gets health records for a specific provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="filters">Optional filters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of health records.</returns>
    public async Task<PagedResponse<ProviderHealthRecordDto>> GetProviderHealthRecordsAsync(
        string providerName,
        ProviderHealthFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        _logger?.LogDebug("Getting health records for provider {ProviderName}", providerName);

        try
        {
            var response = await GetAsync<PagedResponse<ProviderHealthRecordDto>>(
                $"{BaseEndpoint}/records/{Uri.EscapeDataString(providerName)}", 
                filters, 
                cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get health records for provider {ProviderName}", providerName);
            throw;
        }
    }

    /// <summary>
    /// Gets all health records across all providers.
    /// </summary>
    /// <param name="filters">Optional filters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of health records.</returns>
    public async Task<PagedResponse<ProviderHealthRecordDto>> GetAllHealthRecordsAsync(
        ProviderHealthFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting all health records");

        try
        {
            var response = await GetAsync<PagedResponse<ProviderHealthRecordDto>>(
                $"{BaseEndpoint}/records", 
                filters, 
                cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get all health records");
            throw;
        }
    }

    #endregion

    #region Health Status

    /// <summary>
    /// Gets the current health status for all providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of all provider health statuses.</returns>
    public async Task<ProviderHealthSummaryDto> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting provider health summary");

        try
        {
            var summary = await GetAsync<ProviderHealthSummaryDto>(
                $"{BaseEndpoint}/summary", 
                cancellationToken: cancellationToken);

            return summary;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get provider health summary");
            throw;
        }
    }

    /// <summary>
    /// Gets the health status for a specific provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider health status.</returns>
    public async Task<ProviderHealthStatusDto> GetProviderHealthStatusAsync(
        string providerName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        _logger?.LogDebug("Getting health status for provider {ProviderName}", providerName);

        try
        {
            var status = await GetAsync<ProviderHealthStatusDto>(
                $"{BaseEndpoint}/status/{Uri.EscapeDataString(providerName)}", 
                cancellationToken: cancellationToken);

            return status;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get health status for provider {ProviderName}", providerName);
            throw;
        }
    }

    /// <summary>
    /// Gets health statistics for all providers.
    /// </summary>
    /// <param name="periodHours">The time period in hours for statistics calculation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Overall provider health statistics.</returns>
    public async Task<ProviderHealthStatisticsDto> GetHealthStatisticsAsync(
        int periodHours = 24, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting health statistics for {PeriodHours} hours", periodHours);

        try
        {
            var statistics = await GetAsync<ProviderHealthStatisticsDto>(
                $"{BaseEndpoint}/statistics", 
                new { periodHours }, 
                cancellationToken);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get health statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets simple status information for a provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Simple provider status.</returns>
    public async Task<ProviderStatus> GetProviderStatusAsync(
        string providerName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        _logger?.LogDebug("Getting simple status for provider {ProviderName}", providerName);

        try
        {
            var status = await GetAsync<ProviderStatus>(
                $"{BaseEndpoint}/simple-status/{Uri.EscapeDataString(providerName)}", 
                cancellationToken: cancellationToken);

            return status;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get simple status for provider {ProviderName}", providerName);
            throw;
        }
    }

    #endregion

    #region Health Actions

    /// <summary>
    /// Triggers a manual health check for a specific provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public async Task<ProviderHealthRecordDto> TriggerHealthCheckAsync(
        string providerName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        _logger?.LogDebug("Triggering manual health check for provider {ProviderName}", providerName);

        try
        {
            var result = await PostAsync<ProviderHealthRecordDto>(
                $"{BaseEndpoint}/check/{Uri.EscapeDataString(providerName)}", 
                cancellationToken: cancellationToken);

            _logger?.LogInformation("Triggered health check for provider {ProviderName}", providerName);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to trigger health check for provider {ProviderName}", providerName);
            throw;
        }
    }

    /// <summary>
    /// Deletes a provider health configuration.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteProviderHealthConfigurationAsync(
        string providerName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        _logger?.LogDebug("Deleting health configuration for provider {ProviderName}", providerName);

        try
        {
            await DeleteAsync($"{BaseEndpoint}/configuration/{Uri.EscapeDataString(providerName)}", cancellationToken);

            _logger?.LogInformation("Deleted health configuration for provider {ProviderName}", providerName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete health configuration for provider {ProviderName}", providerName);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if a provider is currently healthy.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the provider is healthy, false otherwise.</returns>
    public async Task<bool> IsProviderHealthyAsync(string providerName, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await GetProviderHealthStatusAsync(providerName, cancellationToken);
            return status.IsHealthy;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all unhealthy providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of unhealthy providers.</returns>
    public async Task<IEnumerable<ProviderHealthStatusDto>> GetUnhealthyProvidersAsync(CancellationToken cancellationToken = default)
    {
        var summary = await GetHealthSummaryAsync(cancellationToken);
        return summary.Providers.Where(p => !p.IsHealthy);
    }

    /// <summary>
    /// Gets the overall system health percentage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The percentage of healthy providers (0-100).</returns>
    public async Task<double> GetOverallHealthPercentageAsync(CancellationToken cancellationToken = default)
    {
        var summary = await GetHealthSummaryAsync(cancellationToken);
        
        if (summary.TotalProviders == 0)
            return 100.0; // No providers means 100% healthy
        
        return (double)summary.HealthyProviders / summary.TotalProviders * 100.0;
    }

    #endregion
}