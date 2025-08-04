using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ConduitLLM.Core.Services;
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

            // Health checks removed per YAGNI principle

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

            // Health checks removed per YAGNI principle

            return services;
        }
    }
}