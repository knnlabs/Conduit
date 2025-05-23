using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.HealthChecks
{
    /// <summary>
    /// Health check for Admin API connectivity and circuit breaker state.
    /// </summary>
    public class AdminApiHealthCheck : IHealthCheck
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<AdminApiHealthCheck> _logger;

        public AdminApiHealthCheck(
            IAdminApiClient adminApiClient,
            ILogger<AdminApiHealthCheck> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to get system info as a health check
                var systemInfo = await _adminApiClient.GetSystemInfoAsync();
                
                if (systemInfo != null)
                {
                    return HealthCheckResult.Healthy(
                        "Admin API is responsive",
                        new Dictionary<string, object>
                        {
                            ["responseTime"] = "< 1s",
                            ["lastCheck"] = DateTime.UtcNow
                        });
                }
                
                return HealthCheckResult.Unhealthy(
                    "Admin API returned null response");
            }
            catch (Exception ex) when (ex.Message.Contains("Circuit breaker is open"))
            {
                _logger.LogWarning("Admin API health check failed: Circuit breaker is open");
                
                return HealthCheckResult.Unhealthy(
                    "Circuit breaker is open",
                    exception: ex,
                    new Dictionary<string, object>
                    {
                        ["circuitBreakerState"] = "Open",
                        ["lastCheck"] = DateTime.UtcNow
                    });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning("Admin API health check timed out");
                
                return HealthCheckResult.Degraded(
                    "Admin API request timed out",
                    exception: ex,
                    new Dictionary<string, object>
                    {
                        ["timeout"] = true,
                        ["lastCheck"] = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin API health check failed");
                
                return HealthCheckResult.Unhealthy(
                    "Admin API is not accessible",
                    exception: ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["lastCheck"] = DateTime.UtcNow
                    });
            }
        }
    }
}