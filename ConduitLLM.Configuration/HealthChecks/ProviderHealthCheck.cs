using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check for LLM provider connectivity and availability
    /// </summary>
    public class ProviderHealthCheck : IHealthCheck
    {
        private readonly IProviderHealthRepository _providerHealthRepository;
        private readonly ILogger<ProviderHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthCheck"/> class
        /// </summary>
        public ProviderHealthCheck(
            IProviderHealthRepository providerHealthRepository,
            ILogger<ProviderHealthCheck> logger)
        {
            _providerHealthRepository = providerHealthRepository ?? throw new ArgumentNullException(nameof(providerHealthRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs the health check for provider connectivity
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get latest health status for all providers
                var latestStatuses = await _providerHealthRepository.GetAllLatestStatusesAsync();
                
                if (!latestStatuses.Any())
                {
                    return HealthCheckResult.Degraded(
                        "No provider health records available",
                        data: new Dictionary<string, object>
                        {
                            ["reason"] = "No health checks have been performed yet"
                        });
                }

                var recentTime = DateTime.UtcNow.AddMinutes(-5);
                var recentStatuses = latestStatuses.Where(kvp => kvp.Value.TimestampUtc >= recentTime).ToList();
                
                if (!recentStatuses.Any())
                {
                    var lastCheck = latestStatuses.Values.Max(r => r.TimestampUtc);
                    return HealthCheckResult.Degraded(
                        "Provider health records are stale",
                        data: new Dictionary<string, object>
                        {
                            ["lastCheck"] = lastCheck,
                            ["reason"] = "No recent health checks available"
                        });
                }

                var totalProviders = recentStatuses.Count;
                var healthyProviders = recentStatuses.Count(kvp => kvp.Value.Status == ProviderHealthRecord.StatusType.Online);

                var healthPercentage = (double)healthyProviders / totalProviders * 100;

                if (healthPercentage >= 80)
                {
                    return HealthCheckResult.Healthy(
                        $"{healthyProviders}/{totalProviders} providers are healthy",
                        data: new Dictionary<string, object>
                        {
                            ["totalProviders"] = totalProviders,
                            ["healthyProviders"] = healthyProviders,
                            ["healthPercentage"] = healthPercentage
                        });
                }
                else if (healthPercentage >= 50)
                {
                    return HealthCheckResult.Degraded(
                        $"Only {healthyProviders}/{totalProviders} providers are healthy",
                        data: new Dictionary<string, object>
                        {
                            ["totalProviders"] = totalProviders,
                            ["healthyProviders"] = healthyProviders,
                            ["healthPercentage"] = healthPercentage
                        });
                }
                else
                {
                    return HealthCheckResult.Unhealthy(
                        $"Most providers are unhealthy: {healthyProviders}/{totalProviders} healthy",
                        data: new Dictionary<string, object>
                        {
                            ["totalProviders"] = totalProviders,
                            ["healthyProviders"] = healthyProviders,
                            ["healthPercentage"] = healthPercentage
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking provider health");
                
                return HealthCheckResult.Unhealthy(
                    "Failed to check provider health",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }
        }
    }
}