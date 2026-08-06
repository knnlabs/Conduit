using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Extension methods for configuring monitoring services
    /// </summary>
    public static class HealthMonitoringExtensions
    {
        /// <summary>
        /// Adds the operational alert publisher.
        /// </summary>
        /// <remarks>
        /// Metrics collection and alert evaluation belong to the Prometheus/Grafana stack; what
        /// remains here is the business-facing alert entry point.
        /// </remarks>
        public static IServiceCollection AddHealthMonitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Publishes business alerts to logs and Prometheus; Grafana owns alert routing
            services.AddSingleton<IOperationalAlertPublisher, OperationalAlertPublisher>();

            return services;
        }

    }
}
