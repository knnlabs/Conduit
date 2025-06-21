using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;
using ConduitLLM.AdminClient.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for accessing system information and health status through the Admin API.
/// </summary>
public class SystemService : BaseApiClient
{
    private const string BaseEndpoint = "/api/SystemInfo";
    private static readonly TimeSpan DefaultCacheTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HealthCacheTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Initializes a new instance of the SystemService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public SystemService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<SystemService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    /// <summary>
    /// Retrieves comprehensive system information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System information including version, OS, database, and runtime details.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<SystemInfoDto> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string cacheKey = "system-info";
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/info";
                var response = await GetAsync<SystemInfoDto>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved system info - Version: {Version}, OS: {OS}, Database: {Database}", 
                    response.Version?.Version, response.OperatingSystem?.Description, response.Database?.Provider);
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
    /// Retrieves current health status of the system and its components.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status information for the system and all components.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<HealthStatusDto> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string cacheKey = "system-health";
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/health";
                var response = await GetAsync<HealthStatusDto>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved health status - Overall: {Status}, Components: {ComponentCount}", 
                    response.Status, response.Components?.Count ?? 0);
                return response;
            }, HealthCacheTimeout, cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets current system metrics including CPU, memory, disk, and network usage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current system metrics.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<SystemMetricsDto> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Don't cache metrics as they change frequently
            var endpoint = $"{BaseEndpoint}/metrics";
            var response = await GetAsync<SystemMetricsDto>(endpoint, cancellationToken: cancellationToken);
            
            _logger?.LogDebug("Retrieved system metrics - CPU: {CPU}%, Memory: {Memory}%, Disk: {Disk}%", 
                response.CpuUsagePercent, response.Memory?.UsagePercent, response.Disk?.UsagePercent);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Performs a comprehensive health check of all system components.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed health check results for all components.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<HealthStatusDto> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = $"{BaseEndpoint}/health/check";
            var response = await PostAsync<HealthStatusDto>(endpoint, null, cancellationToken);
            
            // Invalidate health cache after performing a fresh check
            InvalidateCache("system-health");
            
            _logger?.LogDebug("Performed health check - Overall: {Status}, Duration: {Duration}", 
                response.Status, response.Duration);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the application version information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Version information including build details.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<VersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string cacheKey = "version-info";
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/version";
                var response = await GetAsync<VersionInfo>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved version info - Version: {Version}, Build: {BuildDate}", 
                    response.Version, response.BuildDate);
                return response;
            }, TimeSpan.FromHours(1), cancellationToken); // Version info rarely changes
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets database connection and usage information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database information and statistics.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string cacheKey = "database-info";
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/database";
                var response = await GetAsync<DatabaseInfo>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved database info - Provider: {Provider}, Connected: {Connected}", 
                    response.Provider, response.IsConnected);
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
    /// Gets current runtime information including uptime and memory usage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime information and statistics.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<RuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Don't cache runtime info as it changes frequently (uptime, memory)
            var endpoint = $"{BaseEndpoint}/runtime";
            var response = await GetAsync<RuntimeInfo>(endpoint, cancellationToken: cancellationToken);
            
            _logger?.LogDebug("Retrieved runtime info - Uptime: {Uptime}, Memory: {Memory} bytes", 
                response.Uptime, response.MemoryUsageBytes);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets record counts for all database tables.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Record counts for various database tables.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<RecordCountsDto> GetRecordCountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string cacheKey = "record-counts";
            
            return await WithCacheAsync(cacheKey, async () =>
            {
                var endpoint = $"{BaseEndpoint}/counts";
                var response = await GetAsync<RecordCountsDto>(endpoint, cancellationToken: cancellationToken);
                
                _logger?.LogDebug("Retrieved record counts - VirtualKeys: {VK}, Providers: {P}, UsageLogs: {UL}", 
                    response.VirtualKeys, response.ProviderCredentials, response.UsageLogs);
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
    /// Checks if the system is healthy (simplified health check).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system is healthy, false otherwise.</returns>
    public async Task<bool> IsSystemHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await GetHealthStatusAsync(cancellationToken);
            return health.Status == HealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check system health");
            return false;
        }
    }

    /// <summary>
    /// Gets a summary of system information for dashboard display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary object containing key system information.</returns>
    /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
    public async Task<object> GetSystemSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get multiple pieces of information in parallel
            var systemInfoTask = GetSystemInfoAsync(cancellationToken);
            var healthTask = GetHealthStatusAsync(cancellationToken);
            var runtimeTask = GetRuntimeInfoAsync(cancellationToken);

            await Task.WhenAll(systemInfoTask, healthTask, runtimeTask);

            var systemInfo = await systemInfoTask;
            var health = await healthTask;
            var runtime = await runtimeTask;

            return new
            {
                Version = systemInfo.Version?.Version ?? "Unknown",
                Status = health.Status.ToString(),
                Uptime = runtime.Uptime,
                MemoryUsageMB = runtime.MemoryUsageBytes / 1024 / 1024,
                DatabaseProvider = systemInfo.Database?.Provider ?? "Unknown",
                DatabaseConnected = systemInfo.Database?.IsConnected ?? false,
                ComponentsHealthy = health.Components?.Count(c => c.Value.Status == HealthStatus.Healthy) ?? 0,
                ComponentsTotal = health.Components?.Count ?? 0,
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (!(ex is ConduitAdminException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Invalidates all system-related cache entries.
    /// </summary>
    public void InvalidateSystemCaches()
    {
        var cacheKeys = new[]
        {
            "system-info",
            "system-health", 
            "version-info",
            "database-info",
            "record-counts"
        };

        foreach (var key in cacheKeys)
        {
            InvalidateCache(key);
        }

        _logger?.LogDebug("Invalidated system cache entries");
    }
}