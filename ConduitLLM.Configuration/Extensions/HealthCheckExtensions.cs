using System;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for configuring health checks
    /// </summary>
    public static class HealthCheckExtensions
    {
        /// <summary>
        /// Adds standard Conduit health checks to the service collection
        /// </summary>
        /// <param name="healthChecksBuilder">The health checks builder</param>
        /// <param name="configuration">The configuration instance</param>
        /// <param name="tags">Optional tags to apply to all health checks</param>
        /// <returns>The health checks builder for chaining</returns>
        public static IHealthChecksBuilder AddConduitHealthChecks(
            this IHealthChecksBuilder healthChecksBuilder,
            IConfiguration configuration,
            string[]? tags = null)
        {
            tags ??= new[] { "ready" };

            // Add database health check
            healthChecksBuilder.AddDbContextCheck<ConfigurationDbContext>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: tags);

            // Add Redis health check if Redis is configured
            var cacheEnabled = configuration.GetValue<bool>("Conduit:CacheEnabled", false);
            var cacheType = configuration.GetValue<string>("Conduit:CacheType", "Memory");
            
            if (cacheEnabled && string.Equals(cacheType, "Redis", StringComparison.OrdinalIgnoreCase))
            {
                var redisConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING") 
                    ?? configuration.GetValue<string>("Conduit:RedisConnectionString");
                
                if (!string.IsNullOrEmpty(redisConnectionString))
                {
                    healthChecksBuilder.AddRedis(
                        redisConnectionString,
                        name: "redis",
                        failureStatus: HealthStatus.Degraded,
                        tags: tags);
                }
            }

            // Add provider health check
            healthChecksBuilder.AddCheck<ProviderHealthCheck>(
                "providers",
                failureStatus: HealthStatus.Degraded,
                tags: tags);

            return healthChecksBuilder;
        }
    }
}