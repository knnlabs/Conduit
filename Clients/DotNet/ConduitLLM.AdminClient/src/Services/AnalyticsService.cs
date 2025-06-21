using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for analytics and reporting through the Admin API.
/// </summary>
public class AnalyticsService : BaseApiClient
{
    private const string BaseEndpoint = "/analytics";
    private const string CostEndpoint = "/analytics/cost";
    private const string UsageEndpoint = "/analytics/usage";
    private const string LogsEndpoint = "/analytics/logs";
    private const int DefaultPageSize = 100;
    private static readonly TimeSpan ShortCacheTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MediumCacheTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the AnalyticsService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public AnalyticsService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<AnalyticsService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Cost Analytics

    /// <summary>
    /// Retrieves cost summary for the specified period.
    /// </summary>
    /// <param name="startDate">Start date for the cost summary.</param>
    /// <param name="endDate">End date for the cost summary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cost summary information.</returns>
    /// <exception cref="ValidationException">Thrown when the date range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CostSummaryDto> GetCostSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(startDate, endDate);

            var parameters = new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var endpoint = $"{CostEndpoint}/summary";
            var cacheKey = GetCacheKey("cost-summary", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<CostSummaryDto>(endpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CostEndpoint}/summary", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves cost trends by period (daily, weekly, monthly).
    /// </summary>
    /// <param name="startDate">Start date for the analysis.</param>
    /// <param name="endDate">End date for the analysis.</param>
    /// <param name="groupBy">How to group the data (hour, day, week, month).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cost trends by period.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CostByPeriodDto> GetCostByPeriodAsync(
        DateTime startDate,
        DateTime endDate,
        AnalyticsGroupBy groupBy = AnalyticsGroupBy.Day,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(startDate, endDate);

            var parameters = new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                groupBy = groupBy.ToString().ToLower()
            };

            var endpoint = $"{CostEndpoint}/by-period";
            var cacheKey = GetCacheKey("cost-by-period", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<CostByPeriodDto>(endpoint, parameters, cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CostEndpoint}/by-period", "GET");
            throw;
        }
    }

    /// <summary>
    /// Generates a cost forecast based on historical data.
    /// </summary>
    /// <param name="forecastDays">Number of days to forecast.</param>
    /// <param name="basedOnDays">Number of historical days to base the forecast on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cost forecast information.</returns>
    /// <exception cref="ValidationException">Thrown when the parameters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<CostForecastDto> GetCostForecastAsync(
        int forecastDays = 30,
        int basedOnDays = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (forecastDays <= 0 || forecastDays > 365)
                throw new ValidationException("Forecast days must be between 1 and 365");

            if (basedOnDays <= 0 || basedOnDays > 365)
                throw new ValidationException("Based on days must be between 1 and 365");

            var parameters = new
            {
                forecastDays,
                basedOnDays
            };

            var endpoint = $"{CostEndpoint}/forecast";
            var cacheKey = GetCacheKey("cost-forecast", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<CostForecastDto>(endpoint, parameters, cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{CostEndpoint}/forecast", "GET");
            throw;
        }
    }

    #endregion

    #region Usage Analytics

    /// <summary>
    /// Retrieves usage metrics for the specified period.
    /// </summary>
    /// <param name="startDate">Start date for the usage metrics.</param>
    /// <param name="endDate">End date for the usage metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Usage metrics information.</returns>
    /// <exception cref="ValidationException">Thrown when the date range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<UsageMetricsDto> GetUsageMetricsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(startDate, endDate);

            var parameters = new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var endpoint = $"{UsageEndpoint}/metrics";
            var cacheKey = GetCacheKey("usage-metrics", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<UsageMetricsDto>(endpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{UsageEndpoint}/metrics", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves usage analytics by model.
    /// </summary>
    /// <param name="startDate">Start date for the analysis.</param>
    /// <param name="endDate">End date for the analysis.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of model usage analytics.</returns>
    /// <exception cref="ValidationException">Thrown when the date range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<ModelUsageDto>> GetModelUsageAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(startDate, endDate);

            var parameters = new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var endpoint = $"{UsageEndpoint}/by-model";
            var cacheKey = GetCacheKey("model-usage", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<ModelUsageDto>>(endpoint, parameters, cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{UsageEndpoint}/by-model", "GET");
            throw;
        }
    }

    /// <summary>
    /// Retrieves usage analytics by virtual key.
    /// </summary>
    /// <param name="startDate">Start date for the analysis.</param>
    /// <param name="endDate">End date for the analysis.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of virtual key usage analytics.</returns>
    /// <exception cref="ValidationException">Thrown when the date range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<KeyUsageDto>> GetKeyUsageAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(startDate, endDate);

            var parameters = new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var endpoint = $"{UsageEndpoint}/by-key";
            var cacheKey = GetCacheKey("key-usage", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<KeyUsageDto>>(endpoint, parameters, cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{UsageEndpoint}/by-key", "GET");
            throw;
        }
    }

    #endregion

    #region Request Logs

    /// <summary>
    /// Retrieves request logs with optional filtering.
    /// </summary>
    /// <param name="filters">Optional filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of request logs matching the filter criteria.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<RequestLogDto>> GetRequestLogsAsync(
        RequestLogFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = BuildLogFilterParameters(filters);
            var cacheKey = GetCacheKey("request-logs", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<RequestLogDto>>(LogsEndpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, LogsEndpoint, "GET");
            throw;
        }
    }

    /// <summary>
    /// Exports request logs in the specified format.
    /// </summary>
    /// <param name="filters">Filter criteria for the export.</param>
    /// <param name="format">Export format (csv, json, xlsx).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported data as a stream.</returns>
    /// <exception cref="ValidationException">Thrown when the format is unsupported.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<Stream> ExportRequestLogsAsync(
        RequestLogFilters? filters = null,
        string format = "csv",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var supportedFormats = new[] { "csv", "json", "xlsx" };
            if (!supportedFormats.Contains(format.ToLower()))
                throw new ValidationException($"Unsupported export format: {format}. Supported formats: {string.Join(", ", supportedFormats)}");

            var parameters = BuildLogFilterParameters(filters);
            var parametersWithFormat = new Dictionary<string, object?>();
            
            // Convert parameters object to dictionary
            foreach (var prop in parameters.GetType().GetProperties())
            {
                parametersWithFormat[prop.Name] = prop.GetValue(parameters);
            }
            parametersWithFormat["format"] = format.ToLower();

            var endpoint = $"{LogsEndpoint}/export";
            
            var response = await HttpClient.GetAsync($"{endpoint}?{BuildQueryString(parametersWithFormat)}", cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response, endpoint, "GET");

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{LogsEndpoint}/export", "GET");
            throw;
        }
    }

    #endregion

    #region Anomaly Detection

    /// <summary>
    /// Retrieves detected anomalies.
    /// </summary>
    /// <param name="startDate">Start date for anomaly detection.</param>
    /// <param name="endDate">End date for anomaly detection.</param>
    /// <param name="severity">Filter by severity level (optional).</param>
    /// <param name="resolved">Filter by resolved status (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of detected anomalies.</returns>
    /// <exception cref="ValidationException">Thrown when the date range is invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<IEnumerable<AnomalyDto>> GetAnomaliesAsync(
        DateTime startDate,
        DateTime endDate,
        AnomalySeverity? severity = null,
        bool? resolved = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(startDate, endDate);

            var parameters = new
            {
                startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                severity = severity?.ToString().ToLower(),
                resolved
            };

            var endpoint = $"{BaseEndpoint}/anomalies";
            var cacheKey = GetCacheKey("anomalies", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<IEnumerable<AnomalyDto>>(endpoint, parameters, cancellationToken),
                ShortCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/anomalies", "GET");
            throw;
        }
    }

    /// <summary>
    /// Marks an anomaly as resolved.
    /// </summary>
    /// <param name="anomalyId">The anomaly ID to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the anomaly is not found.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task ResolveAnomalyAsync(
        string anomalyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(anomalyId))
                throw new ValidationException("Anomaly ID is required");

            var endpoint = $"{BaseEndpoint}/anomalies/{Uri.EscapeDataString(anomalyId)}/resolve";
            await PostAsync(endpoint, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/anomalies/{anomalyId}/resolve", "POST");
            throw;
        }
    }

    #endregion

    #region Advanced Analytics

    /// <summary>
    /// Retrieves comprehensive analytics data with custom filters.
    /// </summary>
    /// <param name="filters">Analytics filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive analytics data.</returns>
    /// <exception cref="ValidationException">Thrown when the filters are invalid.</exception>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<object> GetAdvancedAnalyticsAsync(
        AnalyticsFilters filters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateDateRange(filters.StartDate, filters.EndDate);

            var parameters = new
            {
                startDate = filters.StartDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDate = filters.EndDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                virtualKeyIds = filters.VirtualKeyIds != null ? string.Join(",", filters.VirtualKeyIds) : null,
                models = filters.Models != null ? string.Join(",", filters.Models) : null,
                providers = filters.Providers != null ? string.Join(",", filters.Providers) : null,
                groupBy = filters.GroupBy?.ToString().ToLower(),
                includeMetadata = filters.IncludeMetadata
            };

            var endpoint = $"{BaseEndpoint}/advanced";
            var cacheKey = GetCacheKey("advanced-analytics", parameters);

            return await WithCacheAsync(
                cacheKey,
                () => GetAsync<object>(endpoint, parameters, cancellationToken),
                MediumCacheTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex, $"{BaseEndpoint}/advanced", "GET");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static void ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            throw new ValidationException("Start date must be before end date");

        if (endDate > DateTime.UtcNow.AddDays(1))
            throw new ValidationException("End date cannot be in the future");

        if ((endDate - startDate).TotalDays > 365)
            throw new ValidationException("Date range cannot exceed 365 days");
    }

    private object BuildLogFilterParameters(RequestLogFilters? filters)
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
            startDate = filters.StartDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endDate = filters.EndDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            virtualKeyId = filters.VirtualKeyId,
            model = filters.Model,
            provider = filters.Provider,
            status = filters.Status?.ToString().ToLower(),
            minCost = filters.MinCost,
            maxCost = filters.MaxCost,
            minDuration = filters.MinDuration,
            maxDuration = filters.MaxDuration,
            ipAddress = filters.IpAddress
        };
    }

    private string BuildQueryString(Dictionary<string, object?> parameters)
    {
        var queryParams = new List<string>();

        foreach (var kvp in parameters)
        {
            if (kvp.Value != null)
            {
                var stringValue = kvp.Value.ToString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    queryParams.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(stringValue)}");
                }
            }
        }

        return string.Join("&", queryParams);
    }

    #endregion
}