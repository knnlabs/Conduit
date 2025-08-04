using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Authentication;
using ConduitLLM.Http.Services;
using ConduitLLM.Http.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public partial class Program
{
    public static void ConfigureSignalRServices(WebApplicationBuilder builder)
    {
        // Get Redis connection string from environment
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        var redisConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING");

        if (!string.IsNullOrEmpty(redisUrl))
        {
            try
            {
                redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ParseRedisUrl(redisUrl);
            }
            catch
            {
                // Failed to parse REDIS_URL, will use legacy connection string if available
            }
        }

        // Register VirtualKeyHubFilter for SignalR authentication
        builder.Services.AddScoped<ConduitLLM.Http.Authentication.VirtualKeyHubFilter>();

        // Register rate limit cache service for SignalR
        builder.Services.AddSingleton<ConduitLLM.Http.Services.VirtualKeyRateLimitCache>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.VirtualKeyRateLimitCache>(provider => 
            provider.GetRequiredService<ConduitLLM.Http.Services.VirtualKeyRateLimitCache>());

        // Register SignalR rate limit filter
        builder.Services.AddSingleton<ConduitLLM.Http.Authentication.VirtualKeySignalRRateLimitFilter>();

        // Register SignalR metrics
        builder.Services.AddSingleton<ConduitLLM.Http.Metrics.SignalRMetrics>();
        builder.Services.AddSingleton<ConduitLLM.Http.Metrics.ISignalRMetrics>(sp => sp.GetRequiredService<ConduitLLM.Http.Metrics.SignalRMetrics>());

        // Register SignalR metrics filter
        builder.Services.AddSingleton<ConduitLLM.Http.Filters.SignalRMetricsFilter>();

        // Register SignalR error handling filter
        builder.Services.AddSingleton<ConduitLLM.Http.Filters.SignalRErrorHandlingFilter>();

        // Register SignalR authentication service
        builder.Services.AddScoped<ConduitLLM.Http.Authentication.ISignalRAuthenticationService, ConduitLLM.Http.Authentication.SignalRAuthenticationService>();

        // Register Metrics Aggregation Service and Hub
        builder.Services.AddSingleton<ConduitLLM.Http.Hubs.IMetricsAggregationService, ConduitLLM.Http.Services.MetricsAggregationService>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.MetricsAggregationService>(sp => 
            (ConduitLLM.Http.Services.MetricsAggregationService)sp.GetRequiredService<ConduitLLM.Http.Hubs.IMetricsAggregationService>());

        // Register Business Metrics Background Service
        builder.Services.AddHostedService<ConduitLLM.Http.Services.BusinessMetricsService>();

        // Add SignalR for real-time navigation state updates
        var signalRBuilder = builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
            options.StreamBufferCapacity = 10;
            
            // Add global filters
            options.AddFilter<ConduitLLM.Http.Filters.SignalRMetricsFilter>();
            options.AddFilter<ConduitLLM.Http.Filters.SignalRErrorHandlingFilter>();
            options.AddFilter<ConduitLLM.Http.Authentication.VirtualKeyHubFilter>();
            options.AddFilter<ConduitLLM.Http.Authentication.VirtualKeySignalRRateLimitFilter>();
        });

        // Configure SignalR Redis backplane for horizontal scaling
        // Use dedicated Redis connection string if available, otherwise fall back to main Redis connection
        var signalRRedisConnectionString = builder.Configuration.GetConnectionString("RedisSignalR") ?? redisConnectionString;
        if (!string.IsNullOrEmpty(signalRRedisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(signalRRedisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = new StackExchange.Redis.RedisChannel("conduit_signalr:", StackExchange.Redis.RedisChannel.PatternMode.Literal);
                options.Configuration.DefaultDatabase = 2; // Separate database for SignalR
            });
            Console.WriteLine("[Conduit] SignalR configured with Redis backplane for horizontal scaling");
        }
        else
        {
            Console.WriteLine("[Conduit] SignalR configured without Redis backplane (single-instance mode)");
        }

        // Register navigation state notification service
        builder.Services.AddSingleton<INavigationStateNotificationService, NavigationStateNotificationService>();

        // Register settings refresh service for runtime configuration updates
        builder.Services.AddSingleton<ISettingsRefreshService, SettingsRefreshService>();

        // Register media lifecycle repository
        builder.Services.AddScoped<IMediaLifecycleRepository, MediaLifecycleRepository>();

        // Register video generation notification service
        builder.Services.AddSingleton<IVideoGenerationNotificationService, VideoGenerationNotificationService>();

        // Register image generation notification service
        builder.Services.AddSingleton<IImageGenerationNotificationService, ImageGenerationNotificationService>();

        // Register unified task notification service
        builder.Services.AddSingleton<ITaskNotificationService, TaskNotificationService>();

        // Register virtual key management notification service
        builder.Services.AddSingleton<IVirtualKeyManagementNotificationService, VirtualKeyManagementNotificationService>();

        // Register usage analytics notification service
        builder.Services.AddSingleton<IUsageAnalyticsNotificationService, UsageAnalyticsNotificationService>();

        // Register model discovery notification services
        builder.Services.Configure<NotificationBatchingOptions>(builder.Configuration.GetSection("ConduitLLM:NotificationBatching"));
        builder.Services.AddSingleton<IModelDiscoverySubscriptionManager, ModelDiscoverySubscriptionManager>();
        builder.Services.AddSingleton<INotificationSeverityClassifier, NotificationSeverityClassifier>();
        builder.Services.AddSingleton<IModelDiscoveryNotificationBatcher, ModelDiscoveryNotificationBatcher>();
        builder.Services.AddHostedService<ModelDiscoveryNotificationBatcher>(sp => 
            (ModelDiscoveryNotificationBatcher)sp.GetRequiredService<IModelDiscoveryNotificationBatcher>());

        // Register batch spend update service for optimized Virtual Key operations
        builder.Services.AddSingleton<ConduitLLM.Configuration.Services.BatchSpendUpdateService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Configuration.Services.BatchSpendUpdateService>>();
            var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var redisConnectionFactory = serviceProvider.GetRequiredService<ConduitLLM.Configuration.Services.RedisConnectionFactory>();
            var batchService = new ConduitLLM.Configuration.Services.BatchSpendUpdateService(serviceScopeFactory, redisConnectionFactory, logger);
            
            // Wire up cache invalidation event if Redis cache is available
            var cache = serviceProvider.GetService<ConduitLLM.Core.Interfaces.IVirtualKeyCache>();
            if (cache != null)
            {
                batchService.SpendUpdatesCompleted += async (keyHashes) =>
                {
                    try
                    {
                        await cache.InvalidateVirtualKeysAsync(keyHashes);
                        logger.LogDebug("Cache invalidated for {Count} Virtual Keys after batch spend update", keyHashes.Length);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to invalidate cache after batch spend update");
                    }
                };
            }
            
            return batchService;
        });
        builder.Services.AddSingleton<ConduitLLM.Configuration.Services.IBatchSpendUpdateService>(serviceProvider =>
            serviceProvider.GetRequiredService<ConduitLLM.Configuration.Services.BatchSpendUpdateService>());
        builder.Services.AddHostedService<ConduitLLM.Configuration.Services.BatchSpendUpdateService>(serviceProvider =>
            serviceProvider.GetRequiredService<ConduitLLM.Configuration.Services.BatchSpendUpdateService>());
    }
}