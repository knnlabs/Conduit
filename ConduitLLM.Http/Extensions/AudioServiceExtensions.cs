using System;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Middleware;
using ConduitLLM.Http.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            // Configure HTTP clients for audio services
            services.AddHttpClient<IAudioTranscriptionClient>();
            services.AddHttpClient<ITextToSpeechClient>();
            
            // PrometheusAudioMetricsExporter removed - metrics handled differently now
            
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
            
            return services;
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