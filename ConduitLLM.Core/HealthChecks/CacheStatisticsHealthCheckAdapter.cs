using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.HealthChecks
{
    /// <summary>
    /// Adapter to integrate cache statistics health check with ASP.NET Core health checks.
    /// </summary>
    public class CacheStatisticsHealthCheckAdapter : IHealthCheck
    {
        private readonly IStatisticsHealthCheck _statisticsHealthCheck;

        public CacheStatisticsHealthCheckAdapter(IStatisticsHealthCheck statisticsHealthCheck)
        {
            _statisticsHealthCheck = statisticsHealthCheck ?? throw new ArgumentNullException(nameof(statisticsHealthCheck));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _statisticsHealthCheck.CheckHealthAsync(cancellationToken);

                var data = new Dictionary<string, object>
                {
                    ["ActiveInstances"] = result.ActiveInstances,
                    ["MissingInstances"] = result.MissingInstances,
                    ["RedisConnected"] = result.RedisConnected,
                    ["AggregationLatencyMs"] = result.AggregationLatencyMs,
                    ["RedisMemoryUsageMB"] = result.RedisMemoryUsageBytes / (1024.0 * 1024.0),
                    ["LastSuccessfulAggregation"] = result.LastSuccessfulAggregation?.ToString("O") ?? "Never"
                };

                // Add component health to data
                foreach (var component in result.ComponentHealth)
                {
                    data[$"Component_{component.Key}_Status"] = component.Value.Status.ToString();
                    if (!string.IsNullOrEmpty(component.Value.Message))
                    {
                        data[$"Component_{component.Key}_Message"] = component.Value.Message;
                    }
                }

                return result.Status switch
                {
                    Interfaces.HealthStatus.Healthy => HealthCheckResult.Healthy(
                        description: "Cache statistics system is healthy",
                        data: data),
                    
                    Interfaces.HealthStatus.Degraded => HealthCheckResult.Degraded(
                        description: string.Join("; ", result.Messages),
                        data: data),
                    
                    _ => HealthCheckResult.Unhealthy(
                        description: string.Join("; ", result.Messages),
                        data: data)
                };
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    description: "Failed to check cache statistics health",
                    exception: ex);
            }
        }
    }
}