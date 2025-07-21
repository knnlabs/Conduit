using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Health check for the cache manager.
    /// </summary>
    public class CacheManagerHealthCheck : IHealthCheck
    {
        private readonly ICacheManager _cacheManager;

        public CacheManagerHealthCheck(ICacheManager cacheManager)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var healthStatus = await _cacheManager.GetHealthStatusAsync();
                
                if (healthStatus.IsHealthy)
                {
                    var data = new Dictionary<string, object>
                    {
                        ["memoryCacheResponseTime"] = healthStatus.MemoryCacheResponseTime?.TotalMilliseconds ?? 0,
                        ["distributedCacheResponseTime"] = healthStatus.DistributedCacheResponseTime?.TotalMilliseconds ?? 0,
                        ["components"] = healthStatus.ComponentStatus
                    };

                    return HealthCheckResult.Healthy("Cache manager is healthy", data);
                }
                else
                {
                    var data = new Dictionary<string, object>
                    {
                        ["issues"] = healthStatus.Issues,
                        ["components"] = healthStatus.ComponentStatus
                    };

                    return HealthCheckResult.Unhealthy("Cache manager has issues", data: data);
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Cache manager health check failed", ex);
            }
        }
    }
}