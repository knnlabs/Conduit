using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for monitoring the health status of the Admin API
    /// </summary>
    public class AdminApiHealthService : IAdminApiHealthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AdminApiOptions _options;
        private readonly ILogger<AdminApiHealthService> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private bool _isHealthy = false;
        private string _lastErrorMessage = string.Empty;
        private DateTime _lastChecked = DateTime.MinValue;
        private TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private DetailedHealthStatus? _lastDetailedStatus = null;

        /// <summary>
        /// Gets whether the Admin API is healthy
        /// </summary>
        public bool IsHealthy => _isHealthy;

        /// <summary>
        /// Gets the last error message, if any
        /// </summary>
        public string LastErrorMessage => _lastErrorMessage;

        /// <summary>
        /// Gets the time when the health was last checked
        /// </summary>
        public DateTime LastChecked => _lastChecked;

        /// <summary>
        /// Gets the last detailed health status
        /// </summary>
        public DetailedHealthStatus? LastDetailedStatus => _lastDetailedStatus;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminApiHealthService"/> class
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory</param>
        /// <param name="options">The Admin API options</param>
        /// <param name="logger">The logger</param>
        public AdminApiHealthService(
            IHttpClientFactory httpClientFactory,
            IOptions<AdminApiOptions> options,
            ILogger<AdminApiHealthService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks the health of the Admin API
        /// </summary>
        /// <param name="force">Whether to force a check regardless of the interval</param>
        /// <returns>True if the Admin API is healthy, false otherwise</returns>
        public async Task<bool> CheckHealthAsync(bool force = false)
        {
            try
            {
                // Use a semaphore to prevent multiple concurrent checks
                await _semaphore.WaitAsync();

                // Check if we should perform a health check
                if (!force && DateTime.UtcNow - _lastChecked < _checkInterval)
                {
                    return _isHealthy;
                }

                // Only actually perform the health check if Admin API is enabled
                if (!_options.UseAdminApi)
                {
                    _isHealthy = false;
                    _lastErrorMessage = "Admin API is disabled";
                    _lastChecked = DateTime.UtcNow;
                    return false;
                }

                // Create HTTP client and configure request
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                var url = $"{_options.BaseUrl.TrimEnd('/')}/health";
                _logger.LogDebug("Checking Admin API health at {Url}", url);

                // Send request to health endpoint
                var response = await httpClient.GetAsync(url);

                // Check if response is successful
                _isHealthy = response.IsSuccessStatusCode;
                _lastChecked = DateTime.UtcNow;

                if (_isHealthy)
                {
                    _lastErrorMessage = string.Empty;
                    _logger.LogInformation("Admin API health check succeeded");
                    
                    // Try to parse detailed health response
                    try
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        _lastDetailedStatus = ParseHealthResponse(content);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse detailed health response");
                    }
                }
                else
                {
                    _lastErrorMessage = $"Health check failed with status code {response.StatusCode}";
                    _logger.LogWarning("Admin API health check failed: {ErrorMessage}", _lastErrorMessage);
                    _lastDetailedStatus = null;
                }

                return _isHealthy;
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                _lastChecked = DateTime.UtcNow;
                _lastErrorMessage = ex.Message;

                _logger.LogError(ex, "Error checking Admin API health: {ErrorMessage}", ex.Message);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Sets the interval between health checks
        /// </summary>
        /// <param name="interval">The interval</param>
        public void SetCheckInterval(TimeSpan interval)
        {
            if (interval < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException("Interval must be at least 1 second", nameof(interval));
            }

            _checkInterval = interval;
        }

        /// <summary>
        /// Gets detailed health status from the Admin API
        /// </summary>
        /// <param name="force">Whether to force a check regardless of the interval</param>
        /// <returns>Detailed health status</returns>
        public async Task<DetailedHealthStatus?> GetDetailedHealthAsync(bool force = false)
        {
            await CheckHealthAsync(force);
            return _lastDetailedStatus;
        }

        /// <summary>
        /// Gets the connection details for the Admin API
        /// </summary>
        /// <returns>Connection details</returns>
        public AdminApiConnectionDetails GetConnectionDetails()
        {
            return new AdminApiConnectionDetails
            {
                BaseUrl = _options.BaseUrl,
                UseAdminApi = _options.UseAdminApi,
                LastChecked = _lastChecked,
                IsHealthy = _isHealthy,
                LastErrorMessage = _lastErrorMessage
            };
        }

        private DetailedHealthStatus? ParseHealthResponse(string content)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;

                var status = new DetailedHealthStatus
                {
                    Status = root.GetProperty("status").GetString() ?? "Unknown",
                    TotalDuration = root.GetProperty("totalDuration").GetDouble(),
                    Timestamp = DateTime.UtcNow
                };

                if (root.TryGetProperty("checks", out var checksElement))
                {
                    foreach (var check in checksElement.EnumerateArray())
                    {
                        var healthCheck = new HealthCheckResult
                        {
                            Name = check.GetProperty("name").GetString() ?? "Unknown",
                            Status = check.GetProperty("status").GetString() ?? "Unknown",
                            Duration = check.TryGetProperty("duration", out var duration) ? duration.GetDouble() : 0
                        };

                        if (check.TryGetProperty("description", out var desc))
                        {
                            healthCheck.Description = desc.GetString();
                        }

                        if (check.TryGetProperty("exception", out var ex))
                        {
                            healthCheck.Exception = ex.GetString();
                        }

                        if (check.TryGetProperty("data", out var data))
                        {
                            healthCheck.Data = new Dictionary<string, object>();
                            foreach (var prop in data.EnumerateObject())
                            {
                                healthCheck.Data[prop.Name] = prop.Value.ToString();
                            }
                        }

                        status.Checks.Add(healthCheck);
                    }
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse health response");
                return null;
            }
        }
    }

    /// <summary>
    /// Details about the Admin API connection
    /// </summary>
    public class AdminApiConnectionDetails
    {
        /// <summary>
        /// Gets or sets the base URL of the Admin API
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the Admin API is being used
        /// </summary>
        public bool UseAdminApi { get; set; }


        /// <summary>
        /// Gets or sets when the health was last checked
        /// </summary>
        public DateTime LastChecked { get; set; }

        /// <summary>
        /// Gets or sets whether the Admin API is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the last error message, if any
        /// </summary>
        public string LastErrorMessage { get; set; } = string.Empty;
    }
}
