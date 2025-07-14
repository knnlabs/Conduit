using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.HealthChecks;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Caching
{
    /// <summary>
    /// Extension methods for registering cache monitoring services
    /// </summary>
    public static class CacheMonitoringExtensions
    {
        /// <summary>
        /// Adds cache monitoring and alerting services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">Configuration for alert thresholds</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCacheMonitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Register the monitoring service as both IHostedService and ICacheMonitoringService
            services.AddSingleton<CacheMonitoringService>();
            services.AddSingleton<ICacheMonitoringService>(provider => provider.GetRequiredService<CacheMonitoringService>());
            services.AddHostedService(provider => provider.GetRequiredService<CacheMonitoringService>());

            // Configure alert thresholds from configuration
            services.Configure<ConduitLLM.Core.Services.MonitoringAlertThresholds>(configuration.GetSection("CacheMonitoring:AlertThresholds"));

            // Replace the existing cache health check with the enhanced version
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                // Remove existing cache manager health check if present
                var existingCheck = options.Registrations.FirstOrDefault(r => r.Name == "cache_manager");
                if (existingCheck != null)
                {
                    options.Registrations.Remove(existingCheck);
                }

                // Add the enhanced cache health check
                options.Registrations.Add(new HealthCheckRegistration(
                    "cache",
                    provider => new CacheHealthCheck(
                        provider.GetRequiredService<ICacheManager>(),
                        provider.GetRequiredService<ICacheMonitoringService>(),
                        provider.GetRequiredService<ICacheMetricsService>(),
                        provider.GetRequiredService<ICacheRegistry>(),
                        provider.GetRequiredService<ILogger<CacheHealthCheck>>()),
                    HealthStatus.Unhealthy,
                    new[] { "cache", "infrastructure" }));
            });

            return services;
        }

        /// <summary>
        /// Adds cache monitoring with custom alert thresholds
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureThresholds">Action to configure alert thresholds</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddCacheMonitoring(
            this IServiceCollection services, 
            Action<ConduitLLM.Core.Services.MonitoringAlertThresholds> configureThresholds)
        {
            // Register the monitoring service
            services.AddSingleton<CacheMonitoringService>();
            services.AddSingleton<ICacheMonitoringService>(provider => provider.GetRequiredService<CacheMonitoringService>());
            services.AddHostedService(provider => provider.GetRequiredService<CacheMonitoringService>());

            // Configure alert thresholds
            services.Configure<ConduitLLM.Core.Services.MonitoringAlertThresholds>(configureThresholds);

            // Add the enhanced cache health check
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                // Remove existing cache manager health check if present
                var existingCheck = options.Registrations.FirstOrDefault(r => r.Name == "cache_manager");
                if (existingCheck != null)
                {
                    options.Registrations.Remove(existingCheck);
                }

                options.Registrations.Add(new HealthCheckRegistration(
                    "cache",
                    provider => new CacheHealthCheck(
                        provider.GetRequiredService<ICacheManager>(),
                        provider.GetRequiredService<ICacheMonitoringService>(),
                        provider.GetRequiredService<ICacheMetricsService>(),
                        provider.GetRequiredService<ICacheRegistry>(),
                        provider.GetRequiredService<ILogger<CacheHealthCheck>>()),
                    HealthStatus.Unhealthy,
                    new[] { "cache", "infrastructure" }));
            });

            return services;
        }
    }
}