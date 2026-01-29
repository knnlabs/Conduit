using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Services;
using MassTransit;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering media generation services
/// </summary>
public static class MediaGenerationExtensions
{
    /// <summary>
    /// Adds media generation services including video generation, retry configuration, metrics, and orchestrators
    /// </summary>
    public static IServiceCollection AddMediaGenerationServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Register Video Generation Service with explicit dependencies
        services.AddScoped<IVideoGenerationService>(sp =>
        {
            var clientFactory = sp.GetRequiredService<ILLMClientFactory>();
            var capabilityService = sp.GetRequiredService<IModelCapabilityService>();
            var costService = sp.GetRequiredService<ICostCalculationService>();
            var virtualKeyService = sp.GetRequiredService<ConduitLLM.Core.Interfaces.IVirtualKeyService>();
            var mediaStorage = sp.GetRequiredService<IMediaStorageService>();
            var taskService = sp.GetRequiredService<IAsyncTaskService>();
            var logger = sp.GetRequiredService<ILogger<VideoGenerationService>>();
            var modelMappingService = sp.GetRequiredService<IModelProviderMappingService>();
            var publishEndpoint = sp.GetService<IPublishEndpoint>(); // Optional
            var taskRegistry = sp.GetService<ICancellableTaskRegistry>(); // Optional

            return new VideoGenerationService(
                clientFactory,
                capabilityService,
                costService,
                virtualKeyService,
                mediaStorage,
                taskService,
                logger,
                modelMappingService,
                publishEndpoint,
                taskRegistry);
        });

        // Configure Video Generation Retry Settings
        services.Configure<VideoGenerationRetryConfiguration>(options =>
        {
            options.MaxRetries = configuration.GetValue<int>("VideoGeneration:MaxRetries", 3);
            options.BaseDelaySeconds = configuration.GetValue<int>("VideoGeneration:BaseDelaySeconds", 30);
            options.MaxDelaySeconds = configuration.GetValue<int>("VideoGeneration:MaxDelaySeconds", 3600);
            options.EnableRetries = configuration.GetValue<bool>("VideoGeneration:EnableRetries", true);
            options.RetryCheckIntervalSeconds = configuration.GetValue<int>("VideoGeneration:RetryCheckIntervalSeconds", 30);
        });

        // Register Image Generation Retry Configuration
        services.Configure<ImageGenerationRetryConfiguration>(
            configuration.GetSection("ConduitLLM:ImageGenerationRetry"));

        // Add background services for monitoring and cleanup (skip in test environment to prevent endless loops)
        if (environment.EnvironmentName != "Test")
        {
            // Register media generation metrics
            services.AddSingleton<MediaGenerationMetrics>();

            // Register media generation orchestrators
            services.AddScoped<ImageGenerationOrchestrator>();
            services.AddScoped<VideoGenerationOrchestrator>();
        }

        Console.WriteLine("[Conduit] Image generation configured with database-first architecture");
        Console.WriteLine("[Conduit] Image generation supports multi-instance deployment with lease-based task processing");
        Console.WriteLine("[Conduit] Image generation performance tracking and optimization enabled");

        return services;
    }
}
