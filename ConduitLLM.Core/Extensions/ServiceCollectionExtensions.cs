using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring ConduitLLM Core services in an IServiceCollection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the ConduitLLM Context Window Management services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConduitContextManagement(this IServiceCollection services, IConfiguration configuration)
        {
            // Register configuration options
            services.Configure<ContextManagementOptions>(
                configuration.GetSection("ConduitLLM:ContextManagement"));

            // Register model capability service - use database-backed implementation
            services.TryAddScoped<IModelCapabilityService, DatabaseModelCapabilityService>();

            // Register token counter - changed to Scoped to match IModelCapabilityService lifetime
            services.AddScoped<ITokenCounter, TiktokenCounter>();

            // Register context manager
            services.AddScoped<IContextManager, ContextManager>();

            return services;
        }

        /// <summary>
        /// Adds the ConduitLLM Audio services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConduitAudioServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register model capability service if not already registered - use database-backed implementation
            services.TryAddScoped<IModelCapabilityService, DatabaseModelCapabilityService>();

            // Register audio capability detector
            services.AddScoped<IAudioCapabilityDetector, AudioCapabilityDetector>();

            // Register audio router
            services.AddScoped<IAudioRouter, AudioRouter>();

            // Register capability detector if not already registered
            services.TryAddScoped<IModelCapabilityDetector, ModelCapabilityDetector>();

            // Register hybrid audio service for STT-LLM-TTS pipeline
            services.AddScoped<IHybridAudioService, HybridAudioService>();

            // Register audio processing service for format conversion, compression, etc.
            services.AddScoped<IAudioProcessingService, AudioProcessingService>();


            // Register security services
            services.AddScoped<IAudioContentFilter, AudioContentFilter>();
            services.AddScoped<IAudioPiiDetector, AudioPiiDetector>();
            services.AddScoped<IAudioAuditLogger, AudioAuditLogger>();
            services.AddScoped<IAudioEncryptionService, AudioEncryptionService>();

            // Register performance optimization services
            services.AddMemoryCache(); // For AudioStreamCache
            services.AddSingleton<IAudioConnectionPool, AudioConnectionPool>();
            services.AddScoped<IAudioStreamCache, AudioStreamCache>();
            services.AddScoped<IAudioCdnService, AudioCdnService>();

            // Register monitoring and observability services
            services.AddSingleton<IAudioMetricsCollector, AudioMetricsCollector>();
            services.AddSingleton<IAudioAlertingService, AudioAlertingService>();
            services.AddSingleton<IAudioTracingService, AudioTracingService>();
            services.AddSingleton<IAudioQualityTracker, AudioQualityTracker>();
            services.AddScoped<MonitoringAudioService>();

            // Register configuration options
            services.Configure<AudioConnectionPoolOptions>(
                configuration.GetSection("ConduitLLM:Audio:ConnectionPool"));
            services.Configure<AudioCacheOptions>(
                configuration.GetSection("ConduitLLM:Audio:Cache"));
            services.Configure<AudioCdnOptions>(
                configuration.GetSection("ConduitLLM:Audio:Cdn"));
            services.Configure<AudioMetricsOptions>(
                configuration.GetSection("ConduitLLM:Audio:Metrics"));
            services.Configure<AudioAlertingOptions>(
                configuration.GetSection("ConduitLLM:Audio:Alerting"));
            services.Configure<AudioTracingOptions>(
                configuration.GetSection("ConduitLLM:Audio:Tracing"));

            return services;
        }
    }
}
