using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Extension methods for configuring monitoring services
    /// </summary>
    public static class HealthMonitoringExtensions
    {
        /// <summary>
        /// Adds distributed monitoring services with Redis-based storage for multi-instance consistency
        /// </summary>
        public static IServiceCollection AddHealthMonitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Publishes business alerts to logs and Prometheus; Grafana owns alert routing
            services.AddSingleton<IOperationalAlertPublisher, OperationalAlertPublisher>();

            // Register distributed SignalR metrics service
            services.AddSingleton<IDistributedSignalRMetricsService, DistributedSignalRMetricsService>();
            services.AddHostedService<DistributedSignalRMetricsService>(provider =>
                provider.GetRequiredService<IDistributedSignalRMetricsService>() as DistributedSignalRMetricsService
                ?? throw new InvalidOperationException("DistributedSignalRMetricsService not registered correctly"));

            // Register security event monitoring services
            services.AddSingleton<ISecurityEventMonitoringService, ConduitLLM.Security.Services.SecurityEventMonitoringService>();
            services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));

            // Register security event monitoring as hosted service
            services.AddHostedService<ConduitLLM.Security.Services.SecurityEventMonitoringService>(provider =>
                provider.GetRequiredService<ISecurityEventMonitoringService>() as ConduitLLM.Security.Services.SecurityEventMonitoringService
                ?? throw new InvalidOperationException("SecurityEventMonitoringService not registered correctly"));

            return services;
        }

    }
}
