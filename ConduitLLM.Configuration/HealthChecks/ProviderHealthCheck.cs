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
    /// Health check for LLM provider connectivity and availability.
    /// </summary>
    public class ProviderHealthCheck : IHealthCheck
    {
        private readonly IProviderHealthRepository _providerHealthRepository;
        private readonly IProviderCredentialRepository _providerCredentialRepository;
        private readonly ILogger<ProviderHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthCheck"/> class.
        /// </summary>
        /// <param name="providerHealthRepository">Provider health repository.</param>
        /// <param name="providerCredentialRepository">Provider credential repository.</param>
        /// <param name="logger">Logger instance.</param>
        public ProviderHealthCheck(
            IProviderHealthRepository providerHealthRepository,
            IProviderCredentialRepository providerCredentialRepository,
            ILogger<ProviderHealthCheck> logger)
        {
            _providerHealthRepository = providerHealthRepository;
            _providerCredentialRepository = providerCredentialRepository;
            _logger = logger;
        }

        /// <summary>
        /// Performs the health check.
        /// </summary>
        /// <param name="context">Health check context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check result.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all enabled providers
                var providers = await _providerCredentialRepository.GetAllAsync();
                var enabledProviders = providers.Where(p => p.IsEnabled).ToList();

                if (!enabledProviders.Any())
                {
                    // For fresh installations, no providers is expected
                    return HealthCheckResult.Degraded("No enabled providers configured");
                }

                // Get recent health records for each provider
                var healthData = new Dictionary<string, object>();
                var unhealthyProviders = new List<string>();
                var degradedProviders = new List<string>();

                // Use bulk query instead of N individual queries
                var allHealthStatuses = await _providerHealthRepository.GetAllLatestStatusesAsync();
                var healthStatusLookup = allHealthStatuses; // Already a dictionary of provider name -> health record

                foreach (var provider in enabledProviders)
                {
                    if (!healthStatusLookup.TryGetValue(provider.ProviderType.ToString(), out var recentHealth))
                    {
                        healthData[$"{provider.ProviderType}_status"] = "Unknown";
                        degradedProviders.Add(provider.ProviderType.ToString());
                        continue;
                    }

                    healthData[$"{provider.ProviderType}_status"] = recentHealth.Status.ToString();
                    healthData[$"{provider.ProviderType}_lastCheck"] = recentHealth.TimestampUtc;
                    healthData[$"{provider.ProviderType}_responseTime"] = recentHealth.ResponseTimeMs;

                    if (recentHealth.Status == ProviderHealthRecord.StatusType.Offline)
                    {
                        unhealthyProviders.Add(provider.ProviderType.ToString());
                    }
                    else if (recentHealth.ResponseTimeMs > 5000)
                    {
                        degradedProviders.Add(provider.ProviderType.ToString());
                    }
                }

                // Determine overall health status
                if (unhealthyProviders.Any())
                {
                    var message = $"Unhealthy providers: {string.Join(", ", unhealthyProviders)}";
                    if (unhealthyProviders.Count == enabledProviders.Count)
                    {
                        return HealthCheckResult.Unhealthy(message, data: healthData);
                    }
                    return HealthCheckResult.Degraded(message, data: healthData);
                }

                if (degradedProviders.Any())
                {
                    return HealthCheckResult.Degraded(
                        $"Degraded providers: {string.Join(", ", degradedProviders)}",
                        data: healthData);
                }

                return HealthCheckResult.Healthy(
                    $"All {enabledProviders.Count} providers are healthy",
                    data: healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking provider health");
                return HealthCheckResult.Unhealthy(
                    "Error checking provider health",
                    exception: ex);
            }
        }
    }
}
