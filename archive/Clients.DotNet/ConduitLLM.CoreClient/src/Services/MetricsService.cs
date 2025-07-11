using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Constants;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for accessing system metrics and performance data from the Conduit Core API.
/// </summary>
public class MetricsService
{
    private readonly BaseClient _client;
    private readonly ILogger<MetricsService>? _logger;
    private const string BaseEndpoint = ApiEndpoints.Root.Metrics;

    /// <summary>
    /// Initializes a new instance of the MetricsService class.
    /// </summary>
    /// <param name="client">The base client for API communication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public MetricsService(BaseClient client, ILogger<MetricsService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    #region Current Metrics

    /// <summary>
    /// Gets the current comprehensive metrics snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete snapshot of current system metrics.</returns>
    public async Task<MetricsSnapshot> GetCurrentMetricsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting current metrics snapshot");

        try
        {
            var metrics = await _client.GetForServiceAsync<MetricsSnapshot>(BaseEndpoint, cancellationToken: cancellationToken);
            
            _logger?.LogDebug("Retrieved metrics snapshot with {ProviderCount} providers and {VirtualKeyCount} virtual keys",
                metrics?.ProviderHealth?.Count ?? 0, metrics?.Business?.ActiveVirtualKeys ?? 0);
            
            return metrics ?? new MetricsSnapshot();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get current metrics");
            throw;
        }
    }

    /// <summary>
    /// Gets current database connection pool metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database connection pool metrics.</returns>
    public async Task<DatabaseMetrics> GetDatabasePoolMetricsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting database pool metrics");

        try
        {
            var metrics = await _client.GetForServiceAsync<DatabaseMetrics>($"{BaseEndpoint}/database/pool", cancellationToken: cancellationToken);
            
            _logger?.LogDebug("Retrieved database pool metrics: {ActiveConnections}/{MaxConnections} connections ({Utilization:F1}%)",
                metrics?.ActiveConnections ?? 0, metrics?.MaxConnections ?? 0, metrics?.PoolUtilization ?? 0);
            
            return metrics ?? new DatabaseMetrics();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get database pool metrics");
            throw;
        }
    }

    /// <summary>
    /// Gets the raw Prometheus metrics format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prometheus-formatted metrics as a string.</returns>
    public async Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting Prometheus metrics");

        try
        {
            // Note: This assumes the Prometheus endpoint accepts text/plain format
            var response = await _client.HttpClientForServices.GetAsync(BaseEndpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var prometheusData = await response.Content.ReadAsStringAsync(cancellationToken) ?? string.Empty;
            
            _logger?.LogDebug("Retrieved Prometheus metrics ({Size} characters)", prometheusData.Length);
            return prometheusData;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get Prometheus metrics");
            throw;
        }
    }

    #endregion

    #region Historical Metrics

    /// <summary>
    /// Gets historical metrics data for a specified time range.
    /// </summary>
    /// <param name="request">The historical metrics request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical metrics data.</returns>
    public async Task<HistoricalMetricsResponse> GetHistoricalMetricsAsync(
        HistoricalMetricsRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger?.LogDebug("Getting historical metrics from {StartTime} to {EndTime} with interval {Interval}",
            request.StartTime, request.EndTime, request.Interval);

        try
        {
            var response = await _client.PostForServiceAsync<HistoricalMetricsResponse>(
                $"{BaseEndpoint}/historical", request, cancellationToken);
            
            _logger?.LogDebug("Retrieved historical metrics with {SeriesCount} metric series",
                response?.Series?.Count ?? 0);
            
            return response ?? new HistoricalMetricsResponse();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get historical metrics");
            throw;
        }
    }

    /// <summary>
    /// Gets historical metrics for a specific time range with simplified parameters.
    /// </summary>
    /// <param name="startTime">Start time for the metrics query.</param>
    /// <param name="endTime">End time for the metrics query.</param>
    /// <param name="metricNames">Optional list of specific metrics to retrieve.</param>
    /// <param name="interval">Optional interval for data aggregation (default: "5m").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Historical metrics data.</returns>
    public async Task<HistoricalMetricsResponse> GetHistoricalMetricsAsync(
        DateTime startTime,
        DateTime endTime,
        IEnumerable<string>? metricNames = null,
        string interval = "5m",
        CancellationToken cancellationToken = default)
    {
        var request = new HistoricalMetricsRequest
        {
            StartTime = startTime,
            EndTime = endTime,
            MetricNames = metricNames?.ToList() ?? new List<string>(),
            Interval = interval
        };

        return await GetHistoricalMetricsAsync(request, cancellationToken);
    }

    #endregion

    #region Specific Metrics

    /// <summary>
    /// Gets current HTTP performance metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP performance metrics.</returns>
    public async Task<HttpMetrics> GetHttpMetricsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentMetricsAsync(cancellationToken);
        return snapshot.Http;
    }

    /// <summary>
    /// Gets current business metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Business metrics including costs and usage.</returns>
    public async Task<BusinessMetrics> GetBusinessMetricsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentMetricsAsync(cancellationToken);
        return snapshot.Business;
    }

    /// <summary>
    /// Gets current system resource metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System resource metrics.</returns>
    public async Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentMetricsAsync(cancellationToken);
        return snapshot.System;
    }

    /// <summary>
    /// Gets current infrastructure component metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Infrastructure metrics including database, Redis, and messaging.</returns>
    public async Task<InfrastructureMetrics> GetInfrastructureMetricsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentMetricsAsync(cancellationToken);
        return snapshot.Infrastructure;
    }

    /// <summary>
    /// Gets current provider health status for all providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of provider health statuses.</returns>
    public async Task<List<ProviderHealthStatus>> GetProviderHealthAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentMetricsAsync(cancellationToken);
        return snapshot.ProviderHealth;
    }

    /// <summary>
    /// Gets health status for a specific provider.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider health status, or null if not found.</returns>
    public async Task<ProviderHealthStatus?> GetProviderHealthAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        var allProviders = await GetProviderHealthAsync(cancellationToken);
        return allProviders.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Analysis and Aggregation

    /// <summary>
    /// Gets the top performing models by request volume.
    /// </summary>
    /// <param name="count">Number of top models to return (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top performing models ordered by request volume.</returns>
    public async Task<List<ModelUsageStats>> GetTopModelsByRequestVolumeAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be greater than 0", nameof(count));

        var metrics = await GetBusinessMetricsAsync(cancellationToken);
        return metrics.ModelUsage
            .OrderByDescending(m => m.RequestsPerMinute)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets the top spending virtual keys.
    /// </summary>
    /// <param name="count">Number of top virtual keys to return (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top spending virtual keys ordered by current spend.</returns>
    public async Task<List<VirtualKeyStats>> GetTopSpendingVirtualKeysAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be greater than 0", nameof(count));

        var metrics = await GetBusinessMetricsAsync(cancellationToken);
        return metrics.VirtualKeyStats
            .OrderByDescending(v => v.CurrentSpend)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets providers that are currently unhealthy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of unhealthy providers.</returns>
    public async Task<List<ProviderHealthStatus>> GetUnhealthyProvidersAsync(CancellationToken cancellationToken = default)
    {
        var allProviders = await GetProviderHealthAsync(cancellationToken);
        return allProviders.Where(p => !p.IsHealthy).ToList();
    }

    /// <summary>
    /// Calculates the overall system health percentage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System health percentage (0-100).</returns>
    public async Task<double> GetSystemHealthPercentageAsync(CancellationToken cancellationToken = default)
    {
        var providers = await GetProviderHealthAsync(cancellationToken);
        if (!providers.Any()) return 100; // If no providers, assume healthy

        var healthyCount = providers.Count(p => p.IsHealthy);
        return (double)healthyCount / providers.Count * 100;
    }

    /// <summary>
    /// Gets the current cost burn rate in USD per hour.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current cost burn rate in USD per hour.</returns>
    public async Task<decimal> GetCurrentCostBurnRateAsync(CancellationToken cancellationToken = default)
    {
        var metrics = await GetBusinessMetricsAsync(cancellationToken);
        return metrics.Cost.CostPerMinute * 60; // Convert per-minute to per-hour
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Checks if the system is currently healthy based on configurable thresholds.
    /// </summary>
    /// <param name="maxErrorRate">Maximum acceptable error rate percentage (default: 5%).</param>
    /// <param name="maxResponseTime">Maximum acceptable P95 response time in milliseconds (default: 2000ms).</param>
    /// <param name="minProviderHealthPercentage">Minimum acceptable provider health percentage (default: 80%).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system is healthy based on the specified thresholds.</returns>
    public async Task<bool> IsSystemHealthyAsync(
        double maxErrorRate = 5.0,
        double maxResponseTime = 2000.0,
        double minProviderHealthPercentage = 80.0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await GetCurrentMetricsAsync(cancellationToken);

            // Check error rate
            if (snapshot.Http.ErrorRate > maxErrorRate)
            {
                _logger?.LogWarning("System unhealthy: Error rate {ErrorRate}% exceeds threshold {MaxErrorRate}%",
                    snapshot.Http.ErrorRate, maxErrorRate);
                return false;
            }

            // Check response time
            if (snapshot.Http.ResponseTimes.P95 > maxResponseTime)
            {
                _logger?.LogWarning("System unhealthy: P95 response time {ResponseTime}ms exceeds threshold {MaxResponseTime}ms",
                    snapshot.Http.ResponseTimes.P95, maxResponseTime);
                return false;
            }

            // Check provider health
            var providerHealth = await GetSystemHealthPercentageAsync(cancellationToken);
            if (providerHealth < minProviderHealthPercentage)
            {
                _logger?.LogWarning("System unhealthy: Provider health {ProviderHealth}% below threshold {MinProviderHealth}%",
                    providerHealth, minProviderHealthPercentage);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check system health");
            return false; // Assume unhealthy if we can't get metrics
        }
    }

    /// <summary>
    /// Gets a summary of key performance indicators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary object with key performance indicators.</returns>
    public async Task<object> GetKPISummaryAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentMetricsAsync(cancellationToken);
        var systemHealth = await GetSystemHealthPercentageAsync(cancellationToken);
        var costBurnRate = await GetCurrentCostBurnRateAsync(cancellationToken);

        return new
        {
            Timestamp = snapshot.Timestamp,
            SystemHealth = new
            {
                OverallHealthPercentage = systemHealth,
                ErrorRate = snapshot.Http.ErrorRate,
                ResponseTimeP95 = snapshot.Http.ResponseTimes.P95,
                ActiveConnections = snapshot.Infrastructure.Database.ActiveConnections,
                DatabaseUtilization = snapshot.Infrastructure.Database.PoolUtilization
            },
            Performance = new
            {
                RequestsPerSecond = snapshot.Http.RequestsPerSecond,
                ActiveRequests = snapshot.Http.ActiveRequests,
                AverageResponseTime = snapshot.Http.ResponseTimes.Average,
                CacheHitRate = snapshot.Infrastructure.Redis.HitRate
            },
            Business = new
            {
                ActiveVirtualKeys = snapshot.Business.ActiveVirtualKeys,
                RequestsPerMinute = snapshot.Business.TotalRequestsPerMinute,
                CostBurnRatePerHour = costBurnRate,
                AverageCostPerRequest = snapshot.Business.Cost.AverageCostPerRequest
            },
            Infrastructure = new
            {
                CpuUsage = snapshot.System.CpuUsagePercent,
                MemoryUsage = snapshot.System.MemoryUsageMB,
                Uptime = snapshot.System.Uptime,
                SignalRConnections = snapshot.Infrastructure.SignalR.ActiveConnections
            }
        };
    }

    #endregion
}