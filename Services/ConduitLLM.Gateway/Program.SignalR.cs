using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.Interfaces;
using Microsoft.AspNetCore.SignalR;

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
        builder.Services.AddScoped<ConduitLLM.Gateway.Authentication.VirtualKeyHubFilter>();

        // Register rate limit cache service for SignalR - with leader election
        builder.Services.AddSingleton<ConduitLLM.Gateway.Services.VirtualKeyRateLimitCache>();
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Gateway.Services.VirtualKeyRateLimitCache>(
            provider => provider.GetRequiredService<ConduitLLM.Gateway.Services.VirtualKeyRateLimitCache>(),
            "VirtualKeyRateLimitCache");

        // Register Redis-based distributed rate limiting services
        // Check if Redis is available
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Register the Redis-based virtual key rate limit service
            builder.Services.AddSingleton<ConduitLLM.Core.Services.IVirtualKeyRateLimitService, ConduitLLM.Core.Services.RedisVirtualKeyRateLimitService>();
            
            // Register the Redis-based SignalR rate limit service
            builder.Services.AddSingleton<ConduitLLM.Core.Services.ISignalRRateLimitService, ConduitLLM.Core.Services.RedisSignalRRateLimitService>();
            
            // Register webhook metrics service (required for distributed tracking)
            builder.Services.AddSingleton<ConduitLLM.Core.Services.IWebhookMetricsService, ConduitLLM.Core.Services.RedisWebhookMetricsService>();
            
            Console.WriteLine("[Conduit] SignalR configured with Redis-based distributed rate limiting");
        }
        else
        {
            // If no Redis, create a warning and provide a fallback
            builder.Services.AddSingleton<ConduitLLM.Core.Services.ISignalRRateLimitService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("No Redis connection configured. SignalR rate limiting will fall back to local memory (security risk in multi-instance deployments)");
                // For now, throw an exception to enforce Redis requirement for rate limiting
                throw new InvalidOperationException("Redis is required for secure distributed rate limiting. Please configure REDIS_URL or CONDUIT_REDIS_CONNECTION_STRING.");
            });
            
            builder.Services.AddSingleton<ConduitLLM.Core.Services.IVirtualKeyRateLimitService>(sp =>
            {
                throw new InvalidOperationException("Redis is required for secure distributed rate limiting. Please configure REDIS_URL or CONDUIT_REDIS_CONNECTION_STRING.");
            });
        }

        // Register SignalR rate limit filter
        builder.Services.AddSingleton<ConduitLLM.Gateway.Authentication.VirtualKeySignalRRateLimitFilter>();

        // Register SignalR metrics
        builder.Services.AddSingleton<ConduitLLM.Gateway.Metrics.SignalRMetrics>();
        builder.Services.AddSingleton<ConduitLLM.Gateway.Interfaces.ISignalRMetrics>(sp => sp.GetRequiredService<ConduitLLM.Gateway.Metrics.SignalRMetrics>());

        // Register SignalR metrics filter
        builder.Services.AddSingleton<ConduitLLM.Gateway.Filters.SignalRMetricsFilter>();

        // Register SignalR error handling filter
        builder.Services.AddSingleton<ConduitLLM.Gateway.Filters.SignalRErrorHandlingFilter>();

        // Register SignalR authentication service
        builder.Services.AddScoped<ConduitLLM.Gateway.Authentication.ISignalRAuthenticationService, ConduitLLM.Gateway.Authentication.SignalRAuthenticationService>();

        // Register Metrics Aggregation Service and Hub - with leader election
        Console.WriteLine("[Service Registration] Registering MetricsAggregationService as singleton...");
        // Use factory to prevent auto-discovery by ASP.NET Core
        builder.Services.AddSingleton<ConduitLLM.Gateway.Hubs.IMetricsAggregationService>(sp =>
        {
            var serviceProvider = sp;
            var logger = sp.GetRequiredService<ILogger<ConduitLLM.Gateway.Services.MetricsAggregationService>>();
            var hubContext = sp.GetRequiredService<IHubContext<ConduitLLM.Gateway.Hubs.MetricsHub>>();
            return new ConduitLLM.Gateway.Services.MetricsAggregationService(serviceProvider, logger, hubContext);
        });
        Console.WriteLine("[Service Registration] Adding leader-elected hosted service for MetricsAggregationService...");
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Gateway.Services.MetricsAggregationService>(
            sp => {
                try
                {
                    Console.WriteLine("[Leader Election] Resolving MetricsAggregationService...");
                    var service = (ConduitLLM.Gateway.Services.MetricsAggregationService)sp.GetRequiredService<ConduitLLM.Gateway.Hubs.IMetricsAggregationService>();
                    Console.WriteLine("[Leader Election] ✓ Successfully resolved MetricsAggregationService");
                    return service;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Leader Election] ✗ FAILED to resolve MetricsAggregationService: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"[Leader Election] Stack trace: {ex.StackTrace}");
                    throw;
                }
            },
            "MetricsAggregationService");

        // Register Business Metrics Background Service - with leader election
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Gateway.Services.BusinessMetricsService>("BusinessMetricsService");

        // Add SignalR for real-time navigation state updates
        var signalRBuilder = builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
            options.StreamBufferCapacity = 10;
            
            // Add global filters
            options.AddFilter<ConduitLLM.Gateway.Filters.SignalRMetricsFilter>();
            options.AddFilter<ConduitLLM.Gateway.Filters.SignalRErrorHandlingFilter>();
            options.AddFilter<ConduitLLM.Gateway.Authentication.VirtualKeyHubFilter>();
            options.AddFilter<ConduitLLM.Gateway.Authentication.VirtualKeySignalRRateLimitFilter>();
        });

        // Add MessagePack protocol support with LZ4 compression
        // Enables both JSON (default) and MessagePack protocols for backward compatibility
        var messagePackEnabled = Environment.GetEnvironmentVariable("SIGNALR_MESSAGEPACK_ENABLED")?.ToLowerInvariant() != "false";
        if (messagePackEnabled)
        {
            signalRBuilder.AddMessagePackProtocol(options =>
            {
                // Configure MessagePack with security and compression
                options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                    .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                    .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData) // CVE-2020-5234 protection
                    .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray) // Use Lz4BlockArray for GC optimization
                    .WithCompressionMinLength(256); // Only compress messages > 256 bytes
            });
            Console.WriteLine("[Conduit] SignalR configured with MessagePack protocol (LZ4 compression enabled)");
            Console.WriteLine("[Conduit] SignalR supports both JSON and MessagePack protocols for backward compatibility");
        }
        else
        {
            Console.WriteLine("[Conduit] SignalR configured with JSON protocol only (MessagePack disabled)");
        }

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

        // Navigation state notification service removed - WebAdmin uses React Query instead of SignalR for model mapping updates

        // Register settings refresh service for runtime configuration updates
        builder.Services.AddSingleton<ISettingsRefreshService, SettingsRefreshService>();

        // MediaLifecycleRepository removed - consolidated into MediaRecordRepository
        // Migration: 20250827194408_ConsolidateMediaTables.cs

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

        // Model discovery notification services removed - capabilities now come from ModelProviderMapping

        // Register billing alerting service for critical failure notifications
        builder.Services.AddSingleton<ConduitLLM.Configuration.Interfaces.IBillingAlertingService, ConduitLLM.Configuration.Services.BillingAlertingService>();

        // Register Redis circuit breaker configuration
        builder.Services.Configure<ConduitLLM.Configuration.Options.RedisCircuitBreakerOptions>(
            builder.Configuration.GetSection("RedisCircuitBreaker"));

        // Register SignalR connection limit configuration
        builder.Services.Configure<SignalRConnectionOptions>(
            builder.Configuration.GetSection(SignalRConnectionOptions.SectionName));

        // Register Redis circuit breaker service
        builder.Services.AddSingleton<ConduitLLM.Configuration.Interfaces.IRedisCircuitBreaker, ConduitLLM.Configuration.Services.RedisCircuitBreaker>();

        // Register batch spend update service for optimized Virtual Key operations
        // Use factory to prevent auto-discovery by ASP.NET Core - register ONLY via interface
        builder.Services.AddSingleton<IBatchSpendUpdateService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Configuration.Services.BatchSpendUpdateService>>();
            var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var redisConnectionFactory = serviceProvider.GetRequiredService<ConduitLLM.Configuration.Services.RedisConnectionFactory>();
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ConduitLLM.Configuration.Options.BatchSpendingOptions>>();
            var alertingService = serviceProvider.GetRequiredService<ConduitLLM.Configuration.Interfaces.IBillingAlertingService>();
            var circuitBreaker = serviceProvider.GetService<ConduitLLM.Configuration.Interfaces.IRedisCircuitBreaker>();
            var batchService = new ConduitLLM.Configuration.Services.BatchSpendUpdateService(serviceScopeFactory, redisConnectionFactory, options, logger, alertingService, circuitBreaker);

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
        Console.WriteLine("[Service Registration] Adding leader-elected hosted service for BatchSpendUpdateService...");
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Configuration.Services.BatchSpendUpdateService>(
            sp => {
                try
                {
                    Console.WriteLine("[Leader Election] Resolving BatchSpendUpdateService...");
                    var service = (ConduitLLM.Configuration.Services.BatchSpendUpdateService)sp.GetRequiredService<IBatchSpendUpdateService>();
                    Console.WriteLine("[Leader Election] ✓ Successfully resolved BatchSpendUpdateService");
                    return service;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Leader Election] ✗ FAILED to resolve BatchSpendUpdateService: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"[Leader Election] Stack trace: {ex.StackTrace}");
                    throw;
                }
            },
            "BatchSpendUpdateService");
    }
}