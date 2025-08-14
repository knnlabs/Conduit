using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using MassTransit;

public partial class Program
{
    public static void ConfigureCachingServices(WebApplicationBuilder builder)
    {
        // Configure batch spending options
        builder.Services.Configure<BatchSpendingOptions>(
            builder.Configuration.GetSection(BatchSpendingOptions.SectionName));

        // Virtual Key service registration will be done after Redis configuration

        // Register cache service based on configuration
        builder.Services.AddCacheService(builder.Configuration);

        // Configure Redis connection for all Redis-dependent services
        // Check for REDIS_URL first, then fall back to CONDUIT_REDIS_CONNECTION_STRING
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
                // Validation will be logged during startup after logger is available
            }
        }

        builder.Services.AddRedisDataProtection(redisConnectionString, "Conduit");

        // Configure distributed cache for async tasks
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Add Redis distributed cache for async task storage
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "conduit-tasks:";
            });
            Console.WriteLine("[Conduit] Configured Redis distributed cache for async task storage");
        }
        else
        {
            // Fall back to in-memory distributed cache
            builder.Services.AddDistributedMemoryCache();
            Console.WriteLine("[Conduit] Using in-memory distributed cache for async task storage (development mode)");
        }

        // Register Virtual Key service with optional Redis caching
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            Console.WriteLine($"[Conduit] Redis connection string configured: {redisConnectionString}");
            
            // Register Redis connection factory for proper connection pooling
            builder.Services.AddSingleton<ConduitLLM.Configuration.Services.RedisConnectionFactory>();
            
            // Use Redis-cached Virtual Key service for high-performance validation
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                Console.WriteLine("[Conduit] Creating Redis connection during service registration...");
                var factory = sp.GetRequiredService<ConduitLLM.Configuration.Services.RedisConnectionFactory>();
                var connectionTask = factory.GetConnectionAsync(redisConnectionString);
                Console.WriteLine("[Conduit] Waiting for Redis connection to complete...");
                var connection = connectionTask.GetAwaiter().GetResult();
                Console.WriteLine("[Conduit] Redis connection established successfully");
                return connection;
            });
            
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IVirtualKeyCache, RedisVirtualKeyCache>();
            
            // Register additional Redis cache services
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IProviderCache, RedisProviderCache>();
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IGlobalSettingCache, RedisGlobalSettingCache>();
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IModelCostCache, RedisModelCostCache>();
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IIpFilterCache, RedisIpFilterCache>();
            
            // Register Redis distributed lock service
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IDistributedLockService, ConduitLLM.Core.Services.RedisDistributedLockService>();
            
            // Register CachedApiVirtualKeyService with event publishing dependency
            builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService>(serviceProvider =>
            {
                var virtualKeyRepository = serviceProvider.GetRequiredService<IVirtualKeyRepository>();
                var spendHistoryRepository = serviceProvider.GetRequiredService<IVirtualKeySpendHistoryRepository>();
                var groupRepository = serviceProvider.GetRequiredService<IVirtualKeyGroupRepository>();
                var cache = serviceProvider.GetRequiredService<ConduitLLM.Core.Interfaces.IVirtualKeyCache>();
                var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional
                var logger = serviceProvider.GetRequiredService<ILogger<CachedApiVirtualKeyService>>();
                
                return new CachedApiVirtualKeyService(virtualKeyRepository, spendHistoryRepository, groupRepository, cache, publishEndpoint, logger);
            });
            
            Console.WriteLine("[Conduit] Using Redis-cached services (high-performance mode) with distributed locking");
            Console.WriteLine("[Conduit] Enabled caches: VirtualKey, Provider, GlobalSetting, ModelCost, IpFilter");
        }
        else
        {
            // Fall back to direct database Virtual Key service
            builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.Http.Services.ApiVirtualKeyService>();
            
            // Register in-memory distributed lock service (for development/single instance)
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IDistributedLockService, ConduitLLM.Core.Services.InMemoryDistributedLockService>();
            
            Console.WriteLine("[Conduit] Using direct database Virtual Key validation (fallback mode) with in-memory locking");
        }

        // Register Webhook Delivery Tracker for deduplication and statistics
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Register the Redis tracker as the inner implementation
            builder.Services.AddSingleton<ConduitLLM.Core.Services.RedisWebhookDeliveryTracker>();
            
            // Add memory caching
            builder.Services.AddMemoryCache();
            
            // Register the cached wrapper as the main interface
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IWebhookDeliveryTracker>(sp =>
            {
                var redisTracker = sp.GetRequiredService<ConduitLLM.Core.Services.RedisWebhookDeliveryTracker>();
                var memoryCache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.CachedWebhookDeliveryTracker>>();
                
                return new ConduitLLM.Core.Services.CachedWebhookDeliveryTracker(redisTracker, memoryCache, logger);
            });
            
            Console.WriteLine("[Conduit] Webhook delivery tracking configured with Redis backend and in-memory cache");
        }
        else
        {
            // If no Redis, log warning and use a no-op implementation
            builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IWebhookDeliveryTracker>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("No Redis connection configured. Webhook delivery tracking and deduplication will not be available.");
                // Return a simple no-op implementation
                return new ConduitLLM.Http.Services.NoOpWebhookDeliveryTracker();
            });
        }
    }
}