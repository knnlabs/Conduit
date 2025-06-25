using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring image generation monitoring and observability.
    /// </summary>
    public static class ImageGenerationMonitoringExtensions
    {
        /// <summary>
        /// Adds comprehensive monitoring and observability for image generation.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddImageGenerationMonitoring(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add metrics collector
            services.AddSingleton<IImageGenerationMetricsCollector, ImageGenerationMetricsCollector>();
            services.Configure<ImageGenerationMetricsOptions>(
                configuration.GetSection("ImageGeneration:Monitoring:Metrics"));
            
            // Add alerting service
            services.AddSingleton<IImageGenerationAlertingService, ImageGenerationAlertingService>();
            services.Configure<ImageGenerationAlertingOptions>(
                configuration.GetSection("ImageGeneration:Monitoring:Alerting"));
            
            // Add analytics service
            services.AddScoped<IImageGenerationAnalyticsService, ImageGenerationAnalyticsService>();
            
            // Add health monitoring
            services.AddHostedService<ImageGenerationHealthMonitor>();
            services.Configure<ImageGenerationHealthOptions>(
                configuration.GetSection("ImageGeneration:Monitoring:Health"));
            
            // Add resilience service (self-healing and failover)
            services.AddHostedService<ImageGenerationResilienceService>();
            services.Configure<ImageGenerationResilienceOptions>(
                configuration.GetSection("ImageGeneration:Monitoring:Resilience"));
            
            // Add Prometheus exporter
            services.AddHostedService<PrometheusImageGenerationMetricsExporter>();
            services.Configure<PrometheusImageGenerationOptions>(
                configuration.GetSection("ImageGeneration:Monitoring:Prometheus"));
            
            // Register default alert rules
            services.AddHostedService<DefaultAlertRulesInitializer>();
            
            return services;
        }
        
        /// <summary>
        /// Adds image generation monitoring with custom options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureMetrics">Action to configure metrics options.</param>
        /// <param name="configureAlerting">Action to configure alerting options.</param>
        /// <param name="configureHealth">Action to configure health monitoring options.</param>
        /// <param name="configureResilience">Action to configure resilience options.</param>
        /// <param name="configurePrometheus">Action to configure Prometheus options.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddImageGenerationMonitoring(
            this IServiceCollection services,
            Action<ImageGenerationMetricsOptions>? configureMetrics = null,
            Action<ImageGenerationAlertingOptions>? configureAlerting = null,
            Action<ImageGenerationHealthOptions>? configureHealth = null,
            Action<ImageGenerationResilienceOptions>? configureResilience = null,
            Action<PrometheusImageGenerationOptions>? configurePrometheus = null)
        {
            // Add core services
            services.AddSingleton<IImageGenerationMetricsCollector, ImageGenerationMetricsCollector>();
            services.AddSingleton<IImageGenerationAlertingService, ImageGenerationAlertingService>();
            services.AddScoped<IImageGenerationAnalyticsService, ImageGenerationAnalyticsService>();
            
            // Configure options
            if (configureMetrics != null)
                services.Configure(configureMetrics);
            
            if (configureAlerting != null)
                services.Configure(configureAlerting);
            
            if (configureHealth != null)
                services.Configure(configureHealth);
            
            if (configureResilience != null)
                services.Configure(configureResilience);
            
            if (configurePrometheus != null)
                services.Configure(configurePrometheus);
            
            // Add hosted services
            services.AddHostedService<ImageGenerationHealthMonitor>();
            services.AddHostedService<ImageGenerationResilienceService>();
            services.AddHostedService<PrometheusImageGenerationMetricsExporter>();
            services.AddHostedService<DefaultAlertRulesInitializer>();
            
            return services;
        }
        
        /// <summary>
        /// Adds basic image generation monitoring (metrics and health only).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddBasicImageGenerationMonitoring(
            this IServiceCollection services)
        {
            services.AddSingleton<IImageGenerationMetricsCollector, ImageGenerationMetricsCollector>();
            services.AddHostedService<ImageGenerationHealthMonitor>();
            
            // Use default options
            services.Configure<ImageGenerationMetricsOptions>(options =>
            {
                options.MetricsRetentionHours = 24;
                options.SlaTargets.MinAvailabilityPercent = 99.9;
                options.SlaTargets.MaxP95ResponseTimeMs = 45000;
                options.SlaTargets.MaxErrorRatePercent = 1.0;
            });
            
            services.Configure<ImageGenerationHealthOptions>(options =>
            {
                options.Enabled = true;
                options.HealthCheckIntervalMinutes = 5;
                options.MetricsEvaluationIntervalMinutes = 1;
            });
            
            return services;
        }
    }
    
    /// <summary>
    /// Initializes default alert rules for image generation monitoring.
    /// </summary>
    internal class DefaultAlertRulesInitializer : IHostedService
    {
        private readonly IImageGenerationAlertingService _alertingService;
        private readonly ILogger<DefaultAlertRulesInitializer> _logger;
        
        public DefaultAlertRulesInitializer(
            IImageGenerationAlertingService alertingService,
            ILogger<DefaultAlertRulesInitializer> logger)
        {
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing default image generation alert rules");
            
            // The default rules are already loaded in the ImageGenerationAlertingService constructor
            // This service could be used to load additional rules from configuration or database
            
            // Example: Register notification channels
            await RegisterDefaultNotificationChannels();
            
            _logger.LogInformation("Default alert rules initialization completed");
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        private async Task RegisterDefaultNotificationChannels()
        {
            // Register a default webhook channel if configured
            var webhookUrl = Environment.GetEnvironmentVariable("CONDUIT_ALERT_WEBHOOK_URL");
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                await _alertingService.RegisterNotificationChannelAsync(new NotificationChannel
                {
                    Name = "Default Webhook",
                    Type = NotificationChannelType.Webhook,
                    Target = webhookUrl,
                    IsEnabled = true,
                    SeverityFilter = new List<AlertSeverity> 
                    { 
                        AlertSeverity.Error, 
                        AlertSeverity.Critical 
                    }
                });
                
                _logger.LogInformation("Registered default webhook notification channel");
            }
            
            // Register Slack channel if configured
            var slackWebhook = Environment.GetEnvironmentVariable("CONDUIT_SLACK_WEBHOOK_URL");
            if (!string.IsNullOrEmpty(slackWebhook))
            {
                await _alertingService.RegisterNotificationChannelAsync(new NotificationChannel
                {
                    Name = "Slack Alerts",
                    Type = NotificationChannelType.Slack,
                    Target = slackWebhook,
                    IsEnabled = true,
                    SeverityFilter = new List<AlertSeverity> 
                    { 
                        AlertSeverity.Warning,
                        AlertSeverity.Error, 
                        AlertSeverity.Critical 
                    }
                });
                
                _logger.LogInformation("Registered Slack notification channel");
            }
        }
    }
}