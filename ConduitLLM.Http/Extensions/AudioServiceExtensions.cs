using System;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.HealthChecks;
using ConduitLLM.Http.Middleware;
using ConduitLLM.Http.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Http.Extensions
{
    /// <summary>
    /// Extension methods for configuring audio services in production.
    /// </summary>
    public static class AudioServiceExtensions
    {
        /// <summary>
        /// Adds production-ready audio services to the service collection.
        /// </summary>
        public static IServiceCollection AddProductionAudioServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add core audio services - most implementations don't exist
            // services.AddScoped<IAudioRouter, DefaultAudioRouter>();
            // services.AddScoped<IAudioProcessingService, AudioProcessingService>();
            // services.AddScoped<IHybridAudioService, HybridAudioService>();
            
            // Add performance services - implementations don't exist
            // services.AddSingleton<IAudioConnectionPool, AudioConnectionPool>();
            // services.AddScoped<IAudioStreamCache, AudioStreamCache>();
            // services.AddScoped<IAudioCdnService, AudioCdnService>();
            // services.AddScoped<PerformanceOptimizedAudioService>();
            
            // Add monitoring services - implementations don't exist
            // services.AddSingleton<IAudioMetricsCollector, AudioMetricsCollector>();
            // services.AddSingleton<IAudioAlertingService, AudioAlertingService>();
            // services.AddSingleton<IAudioTracingService, AudioTracingService>();
            // services.AddScoped<MonitoringAudioService>();
            
            // Add quality tracking - implementation doesn't exist
            // services.AddSingleton<IAudioQualityTracker, AudioQualityTracker>();
            
            // Add correlation services - implementations don't exist
            // services.AddScoped<ICorrelationContextService, CorrelationContextService>();
            // services.AddTransient<CorrelationPropagationHandler>();
            
            // Configure HTTP clients with correlation propagation
            services.AddHttpClient<IAudioTranscriptionClient>();
                // .AddHttpMessageHandler<CorrelationPropagationHandler>();
            
            services.AddHttpClient<ITextToSpeechClient>();
                // .AddHttpMessageHandler<CorrelationPropagationHandler>();
            
            // Add Prometheus metrics exporter - interface doesn't exist
            // services.AddSingleton<IPrometheusAudioMetricsExporter, PrometheusAudioMetricsExporter>();
            services.AddHostedService<PrometheusAudioMetricsExporter>();
            
            // Add graceful shutdown
            services.AddSingleton<IRealtimeSessionManager, RealtimeSessionManager>();
            services.AddHostedService<GracefulShutdownService>();
            
            // Configure options
            services.Configure<AudioConnectionPoolOptions>(
                configuration.GetSection("AudioService:ConnectionPool"));
            services.Configure<AudioCacheOptions>(
                configuration.GetSection("AudioService:Cache"));
            services.Configure<AudioMetricsOptions>(
                configuration.GetSection("AudioService:Monitoring"));
            services.Configure<AudioCdnOptions>(
                configuration.GetSection("AudioService:Cdn"));
            services.Configure<AudioAlertingOptions>(
                configuration.GetSection("AudioService:Monitoring"));
            services.Configure<AudioProviderHealthCheckOptions>(
                configuration.GetSection("HealthChecks:AudioProviders"));
            
            return services;
        }

        /// <summary>
        /// Adds audio service health checks.
        /// </summary>
        public static IHealthChecksBuilder AddAudioHealthChecks(
            this IHealthChecksBuilder builder,
            IConfiguration configuration)
        {
            // Add main audio service health check
            builder.AddTypeActivatedCheck<AudioServiceHealthCheck>(
                "audio-service",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: new[] { "audio", "live", "ready" });
            
            // Add provider-specific health checks
            var providers = configuration.GetSection("AudioService:Providers").GetChildren();
            foreach (var provider in providers)
            {
                var providerName = provider.Key;
                var enabled = provider.GetValue<bool>("Enabled");
                
                if (enabled)
                {
                    builder.AddTypeActivatedCheck<AudioProviderHealthCheck>(
                        $"audio-provider-{providerName.ToLower()}",
                        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                        tags: new[] { "audio", "provider", providerName.ToLower(), "monitoring" },
                        args: new object[] { providerName });
                }
            }
            
            // Add dependency health checks
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                builder.AddRedis(
                    redisConnectionString,
                    name: "audio-cache-redis",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                    tags: new[] { "audio", "cache", "redis" });
            }
            
            return builder;
        }

        /// <summary>
        /// Configures the audio service middleware pipeline.
        /// </summary>
        public static IApplicationBuilder UseProductionAudioServices(
            this IApplicationBuilder app)
        {
            // Add correlation ID middleware
            app.UseMiddleware<CorrelationIdMiddleware>();
            
            // Add Prometheus metrics endpoint
            app.UseMiddleware<PrometheusMetricsMiddleware>();
            
            // Health check endpoints are mapped in Program.cs via MapConduitHealthChecks()
            // to avoid duplicate endpoint registrations
            
            return app;
        }
    }

    /// <summary>
    /// Manages realtime sessions for graceful shutdown.
    /// </summary>
    internal class RealtimeSessionManager : IRealtimeSessionManager
    {
        private readonly IRealtimeAudioClient _realtimeClient;
        private readonly ILogger<RealtimeSessionManager> _logger;

        public RealtimeSessionManager(
            IRealtimeAudioClient realtimeClient,
            ILogger<RealtimeSessionManager> logger)
        {
            _realtimeClient = realtimeClient;
            _logger = logger;
        }

        public async Task<List<RealtimeSessionInfo>> GetActiveSessionsAsync(CancellationToken cancellationToken)
        {
            // This would typically query a session store or tracking service
            _logger.LogInformation("Getting active realtime sessions");
            
            // For now, return empty list as placeholder
            await Task.CompletedTask; // Make it truly async
            return new List<RealtimeSessionInfo>();
        }

        public async Task SendCloseNotificationAsync(string sessionId, string reason, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sending close notification to session {SessionId}: {Reason}", sessionId, reason);
            
            // Implementation would send a WebSocket message to the client
            await Task.CompletedTask;
        }

        public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Forcefully closing session {SessionId}", sessionId);
            
            // Implementation would close the WebSocket connection and clean up resources
            await Task.CompletedTask;
        }
    }
}