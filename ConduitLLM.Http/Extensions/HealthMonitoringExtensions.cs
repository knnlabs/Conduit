using ConduitLLM.Http.Interfaces;
using ConduitLLM.Http.Services;
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
            services.AddScoped<IHealthMonitoringService, Services.HealthMonitoringService>();
            services.AddSingleton<IAlertManagementService, Services.AlertManagementService>();
            
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

            // System resources health check removed per YAGNI principle

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
        /// Adds advanced health monitoring checks (currently empty - removed unnecessary checks)
        /// </summary>
        public static IHealthChecksBuilder AddAdvancedHealthMonitoring(
            this IHealthChecksBuilder healthChecksBuilder,
            IConfiguration configuration)
        {
            // All advanced health checks have been removed per YAGNI principle
            // Basic health checks are sufficient for monitoring service health
            return healthChecksBuilder;
        }
    }
}