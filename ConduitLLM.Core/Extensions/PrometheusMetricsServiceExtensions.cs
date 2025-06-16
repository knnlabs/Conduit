using System;

using ConduitLLM.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring Prometheus metrics services.
    /// </summary>
    public static class PrometheusMetricsServiceExtensions
    {
        /// <summary>
        /// Adds Prometheus metrics exporter for audio operations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddPrometheusAudioMetrics(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure options
            services.Configure<PrometheusExporterOptions>(
                configuration.GetSection("PrometheusMetrics"));

            // Add the exporter as a hosted service
            services.AddSingleton<PrometheusAudioMetricsExporter>();
            services.AddHostedService(provider => provider.GetRequiredService<PrometheusAudioMetricsExporter>());

            return services;
        }

        /// <summary>
        /// Adds Prometheus metrics exporter with custom options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">The options configuration action.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddPrometheusAudioMetrics(
            this IServiceCollection services,
            Action<PrometheusExporterOptions> configureOptions)
        {
            // Configure options
            services.Configure(configureOptions);

            // Add the exporter as a hosted service
            services.AddSingleton<PrometheusAudioMetricsExporter>();
            services.AddHostedService(provider => provider.GetRequiredService<PrometheusAudioMetricsExporter>());

            return services;
        }
    }
}