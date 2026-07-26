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
        /// Adds the operational alert publisher and security event monitoring.
        /// </summary>
        /// <remarks>
        /// Metrics collection and alert evaluation belong to the Prometheus/Grafana stack; what
        /// remains here is the business-facing alert entry point and security event tracking.
        /// </remarks>
        public static IServiceCollection AddHealthMonitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Publishes business alerts to logs and Prometheus; Grafana owns alert routing
            services.AddSingleton<IOperationalAlertPublisher, OperationalAlertPublisher>();

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
