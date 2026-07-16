using ConduitLLM.Configuration.Options;
using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Extension methods for configuring health monitoring services
    /// </summary>
    public static class HealthMonitoringExtensions
    {
        /// <summary>
        /// Adds distributed health monitoring services with Redis-based storage for multi-instance consistency
        /// </summary>
        public static IServiceCollection AddHealthMonitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Register distributed monitoring services (Redis-based for multi-instance consistency)
            services.AddScoped<IHealthMonitoringService, Services.HealthMonitoringService>();
            
            // Register distributed alert management service
            services.AddSingleton<IDistributedAlertManagementService, DistributedAlertManagementService>();
            services.AddSingleton<IAlertManagementService>(provider => 
                provider.GetRequiredService<IDistributedAlertManagementService>());
            services.AddHostedService<DistributedAlertManagementService>(provider =>
                provider.GetRequiredService<IDistributedAlertManagementService>() as DistributedAlertManagementService
                ?? throw new InvalidOperationException("DistributedAlertManagementService not registered correctly"));
            
            // Register distributed performance monitoring service
            services.AddSingleton<IDistributedPerformanceMonitoringService, DistributedPerformanceMonitoringService>();
            services.AddSingleton<IPerformanceMonitoringService>(provider => 
                provider.GetRequiredService<IDistributedPerformanceMonitoringService>());
            services.Configure<PerformanceMonitoringOptions>(configuration.GetSection("PerformanceMonitoring"));
            services.AddHostedService<DistributedPerformanceMonitoringService>(provider =>
                provider.GetRequiredService<IDistributedPerformanceMonitoringService>() as DistributedPerformanceMonitoringService
                ?? throw new InvalidOperationException("DistributedPerformanceMonitoringService not registered correctly"));
            
            // Register distributed SignalR metrics service
            services.AddSingleton<IDistributedSignalRMetricsService, DistributedSignalRMetricsService>();
            services.AddHostedService<DistributedSignalRMetricsService>(provider =>
                provider.GetRequiredService<IDistributedSignalRMetricsService>() as DistributedSignalRMetricsService
                ?? throw new InvalidOperationException("DistributedSignalRMetricsService not registered correctly"));
            
            // Register security event monitoring services
            services.AddSingleton<ISecurityEventMonitoringService, ConduitLLM.Security.Services.SecurityEventMonitoringService>();
            services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));
            
            // Register health monitoring background service
            services.Configure<HealthMonitoringOptions>(configuration.GetSection("HealthMonitoring"));
            services.AddHostedService<HealthMonitoringBackgroundService>();
            
            // Register security event monitoring as hosted service
            services.AddHostedService<ConduitLLM.Security.Services.SecurityEventMonitoringService>(provider => 
                provider.GetRequiredService<ISecurityEventMonitoringService>() as ConduitLLM.Security.Services.SecurityEventMonitoringService
                ?? throw new InvalidOperationException("SecurityEventMonitoringService not registered correctly"));

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

    }
}