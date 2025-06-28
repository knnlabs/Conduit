using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Constants;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Net;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing IP filters through the Admin API.
/// </summary>
public class IpFilterService : BaseApiClient
{
    private const string BaseEndpoint = "/ipfilter";
    private const string SettingsEndpoint = "/ipfilter/settings";
    private const string CheckEndpoint = "/ipfilter/check";
    private const string EnabledEndpoint = "/ipfilter/enabled";
    private const string BulkEndpoint = "/ipfilter/bulk";
    private const int DefaultPageSize = 25;
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(1);
    private static readonly Regex CidrRegex = new(@"^(\d{1,3}\.){3}\d{1,3}\/\d{1,2}$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the IpFilterService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public IpFilterService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<IpFilterService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region CRUD Operations

    /// <summary>
    /// Creates a new IP filter.
    /// </summary>
    /// <param name="request">The IP filter creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created IP filter.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IpFilterDto> CreateAsync(
        CreateIpFilterDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateCreateRequest(request);

            var response = await PostAsync<IpFilterDto>(
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
    /// Retrieves a list of IP filters with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of IP filters matching the filter criteria.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<IpFilterDto>> ListAsync(
        IpFilterFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildFilterParameters(filters);
            var cacheKey = GetCacheKey("ip-filters", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<IpFilterDto>>(BaseEndpoint, parameters, cancellationToken),
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
    /// Retrieves an IP filter by its ID.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IP filter information.</returns>
    /// <exception cref="NotFoundException">Thrown when the filter is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IpFilterDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/{id}";
            var cacheKey = GetCacheKey("ip-filter", id);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IpFilterDto>(endpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/{id}", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all enabled IP filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of enabled IP filters.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<IpFilterDto>> GetEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("ip-filters-enabled");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<IpFilterDto>>(EnabledEndpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, EnabledEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing IP filter.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the filter is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateAsync(
        int id,
        UpdateIpFilterDto request,
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
    /// Deletes an IP filter by its ID.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the filter is not found.</exception>
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

    #region Settings Management

    /// <summary>
    /// Retrieves the IP filter system settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IP filter settings.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IpFilterSettingsDto> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey("ip-filter-settings");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IpFilterSettingsDto>(SettingsEndpoint, cancellationToken: cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, SettingsEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Updates the IP filter system settings.
    /// </summary>
    /// <param name="request">The settings update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task UpdateSettingsAsync(
        UpdateIpFilterSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
                throw new ValidationException("Update request cannot be null");

            await PutAsync(SettingsEndpoint, request, cancellationToken);
            await InvalidateCacheAsync();
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, SettingsEndpoint, "PUT");
            throw;
        }
    }

    #endregion

    #region IP Checking

    /// <summary>
    /// Checks an IP address against the current filter rules.
    /// </summary>
    /// <param name="ipAddress">The IP address to check.</param>
    /// <param name="endpoint">Optional endpoint being accessed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the IP check.</returns>
    /// <exception cref="ValidationException">Thrown when the IP address is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IpCheckResult> CheckIpAsync(
        string ipAddress,
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ValidationException("IP address is required");

            if (!IPAddress.TryParse(ipAddress, out _))
                throw new ValidationException("Invalid IP address format");

            var request = new IpCheckRequest
            {
                IpAddress = ipAddress,
                Endpoint = endpoint
            };

            return await PostAsync<IpCheckResult>(CheckEndpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, CheckEndpoint, "POST");
            throw;
        }
    }

    #endregion

    #region Search and Filtering

    /// <summary>
    /// Searches for IP filters by name or CIDR range.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>IP filters matching the search query.</returns>
    /// <exception cref="ValidationException">Thrown when the query is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<IpFilterDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ValidationException("Search query is required");

        var filters = new IpFilterFilters
        {
            NameContains = query
        };

        return await ListAsync(filters, cancellationToken);
    }

    /// <summary>
    /// Retrieves filters by type (Allow or Deny).
    /// </summary>
    /// <param name="filterType">The filter type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filters of the specified type.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<IpFilterDto>> GetFiltersByTypeAsync(
        FilterType filterType,
        CancellationToken cancellationToken = default)
    {
        var filters = new IpFilterFilters { FilterType = filterType };
        return await ListAsync(filters, cancellationToken);
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Enables an IP filter.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the filter is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task EnableFilterAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await UpdateAsync(id, new UpdateIpFilterDto { IsEnabled = true }, cancellationToken);
    }

    /// <summary>
    /// Disables an IP filter.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the filter is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task DisableFilterAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await UpdateAsync(id, new UpdateIpFilterDto { IsEnabled = false }, cancellationToken);
    }

    /// <summary>
    /// Creates an allow filter for the specified CIDR range.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="cidrRange">The CIDR range.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created allow filter.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IpFilterDto> CreateAllowFilterAsync(
        string name,
        string cidrRange,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateIpFilterDto
        {
            Name = name,
            CidrRange = cidrRange,
            FilterType = FilterType.Allow,
            IsEnabled = true,
            Description = description
        };

        return await CreateAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates a deny filter for the specified CIDR range.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="cidrRange">The CIDR range.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created deny filter.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IpFilterDto> CreateDenyFilterAsync(
        string name,
        string cidrRange,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateIpFilterDto
        {
            Name = name,
            CidrRange = cidrRange,
            FilterType = FilterType.Deny,
            IsEnabled = true,
            Description = description
        };

        return await CreateAsync(request, cancellationToken);
    }

    #endregion

    #region Statistics and Advanced Features

    /// <summary>
    /// Retrieves IP filter statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>IP filter statistics.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<IpFilterStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/statistics";
            var cacheKey = GetCacheKey("ip-filter-statistics");

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IpFilterStatistics>(endpoint, cancellationToken: cancellationToken),
                DefaultCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/statistics", "GET");
            throw;
        }
    }

    /// <summary>
    /// Creates multiple IP filters in a single operation.
    /// </summary>
    /// <param name="request">The bulk filter creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the bulk creation operation.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<BulkIpFilterResponse> BulkCreateAsync(
        BulkIpFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateBulkRequest(request);

            var response = await PostAsync<BulkIpFilterResponse>(
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
    /// Imports IP filters from a file.
    /// </summary>
    /// <param name="fileStream">The file stream containing the filters.</param>
    /// <param name="format">The file format (csv or json).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the import operation.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<BulkIpFilterResponse> ImportFiltersAsync(
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
            content.Add(streamContent, "file", $"filters.{format.ToLower()}");
            content.Add(new StringContent(format.ToLower()), "format");

            var response = await HttpClient.PostAsync(endpoint, content, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response, endpoint, "POST");

            var result = await response.Content.ReadFromJsonAsync<BulkIpFilterResponse>(cancellationToken: cancellationToken);
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
    /// Exports IP filters to a file.
    /// </summary>
    /// <param name="format">The export format (csv or json).</param>
    /// <param name="filterType">Optional filter type to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the exported data.</returns>
    /// <exception cref="ValidationException">Thrown when the format is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<Stream> ExportFiltersAsync(
        string format,
        FilterType? filterType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var supportedFormats = new[] { "csv", "json" };
            if (!supportedFormats.Contains(format?.ToLower()))
                throw new ValidationException($"Unsupported format: {format}. Supported formats: {string.Join(", ", supportedFormats)}");

            var endpoint = $"{BaseEndpoint}/export";
            var queryParams = new List<string> { $"format={Uri.EscapeDataString(format.ToLower())}" };
            
            if (filterType.HasValue)
                queryParams.Add($"filterType={Uri.EscapeDataString(filterType.Value.ToString())}");

            var queryString = string.Join("&", queryParams);
            var response = await HttpClient.GetAsync($"{endpoint}?{queryString}", cancellationToken);
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
    /// Validates a CIDR range format.
    /// </summary>
    /// <param name="cidrRange">The CIDR range to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ValidationException">Thrown when the CIDR range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<IpFilterValidationResult> ValidateCidrAsync(
        string cidrRange,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cidrRange))
                throw new ValidationException("CIDR range is required");

            var endpoint = $"{BaseEndpoint}/validate-cidr";
            var request = new { cidrRange };

            return await PostAsync<IpFilterValidationResult>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/validate-cidr", "POST");
            throw;
        }
    }

    /// <summary>
    /// Tests IP filter rules against an IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to test.</param>
    /// <param name="proposedRules">Optional proposed rules to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The test results.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    /// <exception cref="NotImplementedException">Thrown when the API endpoint is not implemented.</exception>
    public async Task<IpFilterTestResult> TestRulesAsync(
        string ipAddress,
        IEnumerable<CreateIpFilterDto>? proposedRules = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ValidationException("IP address is required");

            if (!IPAddress.TryParse(ipAddress, out _))
                throw new ValidationException("Invalid IP address format");

            var endpoint = $"{BaseEndpoint}/test";
            var request = new
            {
                ipAddress,
                proposedRules = proposedRules?.ToList()
            };

            return await PostAsync<IpFilterTestResult>(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/test", "POST");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static void ValidateCreateRequest(CreateIpFilterDto request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Filter name is required");

        if (request.Name.Length > 100)
            throw new ValidationException("Filter name cannot exceed 100 characters");

        if (string.IsNullOrWhiteSpace(request.CidrRange))
            throw new ValidationException("CIDR range is required");

        if (!CidrRegex.IsMatch(request.CidrRange))
            throw new ValidationException("Invalid CIDR format (e.g., 192.168.1.0/24)");

        if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 500)
            throw new ValidationException("Description cannot exceed 500 characters");
    }

    private static void ValidateUpdateRequest(UpdateIpFilterDto request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (!string.IsNullOrEmpty(request.Name) && request.Name.Length > 100)
            throw new ValidationException("Filter name cannot exceed 100 characters");

        if (!string.IsNullOrEmpty(request.CidrRange) && !CidrRegex.IsMatch(request.CidrRange))
            throw new ValidationException("Invalid CIDR format (e.g., 192.168.1.0/24)");

        if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 500)
            throw new ValidationException("Description cannot exceed 500 characters");
    }

    private static void ValidateBulkRequest(BulkIpFilterRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Filters == null || !request.Filters.Any())
            throw new ValidationException("At least one filter must be provided");

        if (request.Filters.Count() > 100)
            throw new ValidationException("Cannot create more than 100 filters in a single request");

        foreach (var filter in request.Filters)
        {
            ValidateCreateRequest(filter);
        }
    }

    private object BuildFilterParameters(IpFilterFilters? filters)
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
            filterType = filters.FilterType?.ToString(),
            isEnabled = filters.IsEnabled,
            nameContains = filters.NameContains,
            cidrContains = filters.CidrContains,
            lastMatchedAfter = filters.LastMatchedAfter?.ToString(DateFormats.ApiDateTime),
            lastMatchedBefore = filters.LastMatchedBefore?.ToString(DateFormats.ApiDateTime),
            minMatchCount = filters.MinMatchCount
        };
    }

    private async Task InvalidateCacheAsync()
    {
        // Clear all IP filter related cache entries
        await Task.CompletedTask;
    }

    #endregion
}