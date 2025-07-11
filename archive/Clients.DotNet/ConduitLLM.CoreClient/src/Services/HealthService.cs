using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for monitoring system health and performing health checks against the Conduit Core API.
/// </summary>
public class HealthService
{
    private readonly BaseClient _client;
    private readonly ILogger<HealthService>? _logger;

    /// <summary>
    /// Initializes a new instance of the HealthService class.
    /// </summary>
    /// <param name="client">The base client for API communication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public HealthService(BaseClient client, ILogger<HealthService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    #region Basic Health Checks

    /// <summary>
    /// Performs a liveness check to verify the API is responsive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A health check response indicating if the API is alive.</returns>
    public async Task<HealthCheckResponse> GetLivenessAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Performing liveness check");

        try
        {
            // Note: Core API only has a single /health endpoint at root level (no /v1 prefix)
            // The health endpoint doesn't require authentication, so we bypass the normal client methods
            using var httpClient = new HttpClient { BaseAddress = new Uri(_client.Configuration.BaseUrl) };
            var response = await httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var healthResponse = System.Text.Json.JsonSerializer.Deserialize<HealthCheckResponse>(content, jsonOptions) ?? new HealthCheckResponse { Status = "Healthy" };
            
            _logger?.LogDebug("Liveness check completed with status: {Status}", healthResponse.Status);
            return healthResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Liveness check failed");
            return new HealthCheckResponse
            {
                Status = "Unhealthy",
                TotalDuration = 0,
                Checks = new List<HealthCheckItem>
                {
                    new HealthCheckItem
                    {
                        Name = "liveness",
                        Status = "Unhealthy",
                        Description = $"Liveness check failed: {ex.Message}",
                        Duration = 0
                    }
                }
            };
        }
    }

    /// <summary>
    /// Performs a readiness check to verify the API and its dependencies are ready to serve requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A health check response indicating readiness status.</returns>
    public async Task<HealthCheckResponse> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Performing readiness check");

        try
        {
            // Note: Core API only has a single /health endpoint at root level (no /v1 prefix)
            // The health endpoint doesn't require authentication, so we bypass the normal client methods
            using var httpClient = new HttpClient { BaseAddress = new Uri(_client.Configuration.BaseUrl) };
            var response = await httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var healthResponse = System.Text.Json.JsonSerializer.Deserialize<HealthCheckResponse>(content, jsonOptions) ?? new HealthCheckResponse { Status = "Healthy" };
            
            _logger?.LogDebug("Readiness check completed with status: {Status}, Duration: {Duration}ms", 
                healthResponse.Status, healthResponse.TotalDuration);
            return healthResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Readiness check failed");
            return new HealthCheckResponse
            {
                Status = "Unhealthy",
                TotalDuration = 0,
                Checks = new List<HealthCheckItem>
                {
                    new HealthCheckItem
                    {
                        Name = "readiness",
                        Status = "Unhealthy",
                        Description = $"Readiness check failed: {ex.Message}",
                        Duration = 0
                    }
                }
            };
        }
    }

    /// <summary>
    /// Performs a comprehensive health check of all system components.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A detailed health check response for all system components.</returns>
    public async Task<HealthCheckResponse> GetFullHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Performing full health check");

        try
        {
            // Note: Core API health endpoint is at root level (no /v1 prefix and no authentication required)
            using var httpClient = new HttpClient { BaseAddress = new Uri(_client.Configuration.BaseUrl) };
            var response = await httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var healthResponse = System.Text.Json.JsonSerializer.Deserialize<HealthCheckResponse>(content, jsonOptions) ?? new HealthCheckResponse { Status = "Healthy" };
            
            _logger?.LogDebug("Full health check completed with status: {Status}, {CheckCount} checks, Duration: {Duration}ms", 
                healthResponse.Status, healthResponse.Checks?.Count ?? 0, healthResponse.TotalDuration);
            return healthResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Full health check failed");
            return new HealthCheckResponse
            {
                Status = "Unhealthy",
                TotalDuration = 0,
                Checks = new List<HealthCheckItem>
                {
                    new HealthCheckItem
                    {
                        Name = "full_health",
                        Status = "Unhealthy",
                        Description = $"Full health check failed: {ex.Message}",
                        Duration = 0
                    }
                }
            };
        }
    }

    #endregion

    #region Health Status Analysis

    /// <summary>
    /// Checks if the system is currently healthy based on the full health check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system is healthy, false otherwise.</returns>
    public async Task<bool> IsSystemHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await GetFullHealthAsync(cancellationToken);
            return health.Status == "Healthy";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the system is ready to serve requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system is ready, false otherwise.</returns>
    public async Task<bool> IsSystemReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var readiness = await GetReadinessAsync(cancellationToken);
            return readiness.Status == "Healthy";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all unhealthy components from the full health check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of unhealthy health check items.</returns>
    public async Task<IEnumerable<HealthCheckItem>> GetUnhealthyComponentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await GetFullHealthAsync(cancellationToken);
            return health.Checks?.Where(c => c.Status != "Healthy") ?? Enumerable.Empty<HealthCheckItem>();
        }
        catch
        {
            return Enumerable.Empty<HealthCheckItem>();
        }
    }

    /// <summary>
    /// Gets a specific health check by name.
    /// </summary>
    /// <param name="checkName">The name of the health check to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetHealthCheckAsync(string checkName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkName))
            throw new ArgumentException("Check name cannot be null or empty", nameof(checkName));

        try
        {
            var health = await GetFullHealthAsync(cancellationToken);
            return health.Checks?.FirstOrDefault(c => c.Name.Equals(checkName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Component-Specific Health Checks

    /// <summary>
    /// Gets the database health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetDatabaseHealthAsync(CancellationToken cancellationToken = default)
    {
        return await GetHealthCheckAsync("database", cancellationToken);
    }

    /// <summary>
    /// Gets the Redis cache health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redis health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetRedisHealthAsync(CancellationToken cancellationToken = default)
    {
        return await GetHealthCheckAsync("redis", cancellationToken);
    }

    /// <summary>
    /// Gets the RabbitMQ message queue health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RabbitMQ health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetRabbitMQHealthAsync(CancellationToken cancellationToken = default)
    {
        return await GetHealthCheckAsync("rabbitmq", cancellationToken);
    }

    /// <summary>
    /// Gets the provider health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetProvidersHealthAsync(CancellationToken cancellationToken = default)
    {
        return await GetHealthCheckAsync("providers", cancellationToken);
    }

    /// <summary>
    /// Gets the system resources health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System resources health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetSystemResourcesHealthAsync(CancellationToken cancellationToken = default)
    {
        return await GetHealthCheckAsync("system_resources", cancellationToken);
    }

    /// <summary>
    /// Gets the SignalR health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SignalR health check item, or null if not found.</returns>
    public async Task<HealthCheckItem?> GetSignalRHealthAsync(CancellationToken cancellationToken = default)
    {
        return await GetHealthCheckAsync("signalr", cancellationToken);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Waits for the system to become healthy within a specified timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the system to become healthy.</param>
    /// <param name="pollingInterval">Interval between health check polls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system became healthy within the timeout, false otherwise.</returns>
    public async Task<bool> WaitForHealthyAsync(
        TimeSpan timeout, 
        TimeSpan? pollingInterval = null, 
        CancellationToken cancellationToken = default)
    {
        var interval = pollingInterval ?? TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow.Add(timeout);

        _logger?.LogDebug("Waiting for system to become healthy within {Timeout}, polling every {Interval}", 
            timeout, interval);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (await IsSystemHealthyAsync(cancellationToken))
                {
                    _logger?.LogInformation("System became healthy");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Health check failed during wait, continuing...");
            }

            await Task.Delay(interval, cancellationToken);
        }

        _logger?.LogWarning("System did not become healthy within {Timeout}", timeout);
        return false;
    }

    /// <summary>
    /// Waits for the system to become ready within a specified timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the system to become ready.</param>
    /// <param name="pollingInterval">Interval between readiness check polls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system became ready within the timeout, false otherwise.</returns>
    public async Task<bool> WaitForReadyAsync(
        TimeSpan timeout, 
        TimeSpan? pollingInterval = null, 
        CancellationToken cancellationToken = default)
    {
        var interval = pollingInterval ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow.Add(timeout);

        _logger?.LogDebug("Waiting for system to become ready within {Timeout}, polling every {Interval}", 
            timeout, interval);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (await IsSystemReadyAsync(cancellationToken))
                {
                    _logger?.LogInformation("System became ready");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Readiness check failed during wait, continuing...");
            }

            await Task.Delay(interval, cancellationToken);
        }

        _logger?.LogWarning("System did not become ready within {Timeout}", timeout);
        return false;
    }

    /// <summary>
    /// Gets a summary of the current health status with key metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A health summary object.</returns>
    public async Task<object> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await GetFullHealthAsync(cancellationToken);
            var checks = health.Checks ?? new List<HealthCheckItem>();

            var healthyCount = checks.Count(c => c.Status == "Healthy");
            var degradedCount = checks.Count(c => c.Status == "Degraded");
            var unhealthyCount = checks.Count(c => c.Status == "Unhealthy");

            return new
            {
                OverallStatus = health.Status,
                TotalDuration = health.TotalDuration,
                CheckCounts = new
                {
                    Total = checks.Count,
                    Healthy = healthyCount,
                    Degraded = degradedCount,
                    Unhealthy = unhealthyCount
                },
                HealthPercentage = checks.Count > 0 ? (double)healthyCount / checks.Count * 100 : 100,
                Components = checks.Select(c => new
                {
                    c.Name,
                    c.Status,
                    c.Duration,
                    HasData = c.Data?.Any() == true
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get health summary");
            return new
            {
                OverallStatus = "Unhealthy",
                Error = ex.Message,
                TotalDuration = 0,
                CheckCounts = new { Total = 0, Healthy = 0, Degraded = 0, Unhealthy = 1 },
                HealthPercentage = 0.0,
                Components = new List<object>()
            };
        }
    }

    #endregion
}