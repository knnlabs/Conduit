using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using ConduitLLM.Http.Services;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.HealthChecks;
using ConduitLLM.Core.HealthChecks;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Http.Extensions
{
    /// <summary>
    /// Extension methods for configuring health monitoring services
    /// </summary>
    public static class HealthMonitoringExtensions
    {
        /// <summary>
        /// Adds health monitoring services and advanced health checks
        /// </summary>
        public static IServiceCollection AddHealthMonitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Register health monitoring services
            services.AddScoped<Services.IHealthMonitoringService, Services.HealthMonitoringService>();
            services.AddSingleton<Services.IAlertManagementService, Services.AlertManagementService>();
            
            // Register security event monitoring services
            services.AddSingleton<ISecurityEventMonitoringService, ConduitLLM.Security.Services.SecurityEventMonitoringService>();
            services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));
            
            // Register health monitoring background service
            services.Configure<HealthMonitoringOptions>(configuration.GetSection("HealthMonitoring"));
            services.AddHostedService<HealthMonitoringBackgroundService>();
            
            // Register performance monitoring
            services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
            services.Configure<PerformanceMonitoringOptions>(configuration.GetSection("PerformanceMonitoring"));
            services.AddHostedService<PerformanceMonitoringService>(provider => 
                provider.GetRequiredService<IPerformanceMonitoringService>() as PerformanceMonitoringService 
                ?? throw new InvalidOperationException("PerformanceMonitoringService not registered correctly"));
            
            // Register security event monitoring as hosted service
            services.AddHostedService<ConduitLLM.Security.Services.SecurityEventMonitoringService>(provider => 
                provider.GetRequiredService<ISecurityEventMonitoringService>() as ConduitLLM.Security.Services.SecurityEventMonitoringService
                ?? throw new InvalidOperationException("SecurityEventMonitoringService not registered correctly"));

            // Configure system resources health check options
            services.Configure<SystemResourcesHealthCheckOptions>(options =>
            {
                var section = configuration.GetSection("HealthMonitoring:SystemResources");
                if (section.Exists())
                {
                    section.Bind(options);
                }
            });

            // Register notification services
            services.Configure<AlertNotificationOptions>(configuration.GetSection("HealthMonitoring:Notifications"));
            services.Configure<WebhookNotificationOptions>(configuration.GetSection("HealthMonitoring:Notifications:Webhook"));
            services.Configure<EmailNotificationOptions>(configuration.GetSection("HealthMonitoring:Notifications:Email"));
            services.Configure<SlackNotificationOptions>(configuration.GetSection("HealthMonitoring:Notifications:Slack"));

            // Register notification channels
            services.AddSingleton<IAlertNotificationChannel, WebhookNotificationChannel>();
            services.AddSingleton<IAlertNotificationChannel, EmailNotificationChannel>();
            services.AddSingleton<IAlertNotificationChannel, SlackNotificationChannel>();

            // Register notification service
            services.AddSingleton<IAlertNotificationService, AlertNotificationService>();

            // Register batching service if enabled
            var notificationOptions = configuration.GetSection("HealthMonitoring:Notifications").Get<AlertNotificationOptions>();
            if (notificationOptions?.EnableBatching == true)
            {
                services.AddSingleton<AlertBatchingService>();
                services.AddHostedService(provider => provider.GetRequiredService<AlertBatchingService>());
            }

            return services;
        }

        /// <summary>
        /// Adds advanced health monitoring checks for system resources and API endpoints
        /// </summary>
        public static IHealthChecksBuilder AddAdvancedHealthMonitoring(
            this IHealthChecksBuilder healthChecksBuilder,
            IConfiguration configuration)
        {
            // Add system resources health check (excluded from main /health endpoint)
            healthChecksBuilder.AddCheck<SystemResourcesHealthCheck>(
                "system_resources",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "system", "resources", "performance", "monitoring" });

            // Add HTTP connection pool health check (excluded from main /health endpoint)
            healthChecksBuilder.AddCheck<ConduitLLM.Core.HealthChecks.HttpConnectionPoolHealthCheck>(
                "http_connection_pool",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "http", "pool", "performance", "monitoring" });

            // Add SignalR health check if available
            healthChecksBuilder.AddCheck<SignalRHealthCheck>(
                "signalr",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "signalr", "realtime", "ready", "monitoring" });

            // API endpoint health checks removed - causes circular dependencies

            return healthChecksBuilder;
        }
    }
}