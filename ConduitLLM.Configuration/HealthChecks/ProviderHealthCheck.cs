using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check for LLM provider connectivity and availability.
    /// </summary>
    public class ProviderHealthCheck : IHealthCheck
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProviderHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthCheck"/> class.
        /// </summary>
        /// <param name="scopeFactory">Service scope factory for creating scoped services.</param>
        /// <param name="logger">Logger instance.</param>
        public ProviderHealthCheck(
            IServiceScopeFactory scopeFactory,
            ILogger<ProviderHealthCheck> logger)
        {
            _scopeFactory = scopeFactory;
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
                // Create a scope to resolve scoped services
                using var scope = _scopeFactory.CreateScope();
                var providerHealthRepository = scope.ServiceProvider.GetRequiredService<IProviderHealthRepository>();
                var providerRepository = scope.ServiceProvider.GetRequiredService<IProviderRepository>();

                // Get all enabled providers
                var providers = await providerRepository.GetAllAsync();
                var enabledProviders = providers.Where(p => p.IsEnabled).ToList();

                if (!enabledProviders.Any())
                {
                    // For fresh installations, no providers is expected
                    return HealthCheckResult.Degraded("No enabled providers configured");
                }

                // Get recent health records for each provider
                var healthData = new Dictionary<string, object>();
                var unhealthyProviders = new List<(int id, string name)>();
                var degradedProviders = new List<(int id, string name)>();

                // Use bulk query instead of N individual queries
                var allHealthStatuses = await providerHealthRepository.GetAllLatestStatusesAsync();
                var healthStatusLookup = allHealthStatuses; // Already a dictionary of provider ID -> health record

                foreach (var provider in enabledProviders)
                {
                    if (!healthStatusLookup.TryGetValue(provider.Id, out var recentHealth))
                    {
                        healthData[$"provider_{provider.Id}_status"] = "Unknown";
                        healthData[$"provider_{provider.Id}_name"] = provider.ProviderName;
                        degradedProviders.Add((provider.Id, provider.ProviderName));
                        continue;
                    }

                    healthData[$"provider_{provider.Id}_status"] = recentHealth.Status.ToString();
                    healthData[$"provider_{provider.Id}_name"] = provider.ProviderName;
                    healthData[$"provider_{provider.Id}_lastCheck"] = recentHealth.TimestampUtc;
                    healthData[$"provider_{provider.Id}_responseTime"] = recentHealth.ResponseTimeMs;

                    if (recentHealth.Status == ProviderHealthRecord.StatusType.Offline)
                    {
                        unhealthyProviders.Add((provider.Id, provider.ProviderName));
                    }
                    else if (recentHealth.ResponseTimeMs > 5000)
                    {
                        degradedProviders.Add((provider.Id, provider.ProviderName));
                    }
                }

                // Determine overall health status
                if (unhealthyProviders.Any())
                {
                    var providerList = string.Join(", ", unhealthyProviders.Select(p => $"{p.id}:{p.name}"));
                    var message = $"Unhealthy providers: {providerList}";
                    if (unhealthyProviders.Count == enabledProviders.Count)
                    {
                        return HealthCheckResult.Unhealthy(message, data: healthData);
                    }
                    return HealthCheckResult.Degraded(message, data: healthData);
                }

                if (degradedProviders.Any())
                {
                    var providerList = string.Join(", ", degradedProviders.Select(p => $"{p.id}:{p.name}"));
                    return HealthCheckResult.Degraded(
                        $"Degraded providers: {providerList}",
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
