using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering cache manager services.
    /// </summary>
    public static class CacheManagerExtensions
    {
        /// <summary>
        /// Adds the unified cache manager to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheManager(this IServiceCollection services, IConfiguration configuration)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options from configuration
            services.Configure<CacheManagerOptions>(configuration.GetSection("CacheManager"));
            services.Configure<CacheStatisticsOptions>(configuration.GetSection("CacheStatistics"));

            // Register statistics collector (local mode only)
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                return new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    sp.GetService<ICacheStatisticsStore>());
            });

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Health checks removed per YAGNI principle

            return services;
        }

        /// <summary>
        /// Adds the unified cache manager with custom options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheManager(this IServiceCollection services, Action<CacheManagerOptions> configureOptions)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options
            services.Configure(configureOptions);

            // Register statistics collector with default options
            services.AddSingleton<ICacheStatisticsCollector, CacheStatisticsCollector>();

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Health checks removed per YAGNI principle

            return services;
        }

        /// <summary>
        /// Adds the unified cache manager with Redis distributed cache.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="redisConnectionString">Redis connection string.</param>
        /// <param name="configureOptions">Optional action to configure options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheManagerWithRedis(
            this IServiceCollection services, 
            string redisConnectionString,
            Action<CacheManagerOptions>? configureOptions = null)
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Add Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "conduit:cache:";
            });

            // Configure options
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            // Register statistics store for Redis
            services.AddSingleton<ICacheStatisticsStore, RedisCacheStatisticsStore>();

            // Register Redis connection multiplexer with lazy initialization
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<CacheManager>>();
                logger.LogInformation("Creating Redis connection multiplexer (lazy initialization)");
                
                // Parse connection string and set non-blocking options
                var configOptions = ConfigurationOptions.Parse(redisConnectionString);
                configOptions.AbortOnConnectFail = false; // Don't block on startup
                configOptions.ConnectTimeout = 5000; // 5 second timeout
                configOptions.ConnectRetry = 3;
                
                try
                {
                    var multiplexer = ConnectionMultiplexer.Connect(configOptions);
                    logger.LogInformation("Redis connection multiplexer created successfully");
                    return multiplexer;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create Redis connection multiplexer. Cache functionality may be degraded.");
                    throw;
                }
            });

            // Register distributed statistics collector
            services.AddSingleton<IDistributedCacheStatisticsCollector, RedisCacheStatisticsCollector>();
            
            // Statistics health check removed per YAGNI principle

            // Register hybrid collector as the main statistics collector
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                var distributedCollector = sp.GetService<IDistributedCacheStatisticsCollector>();
                var localCollector = new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    sp.GetService<ICacheStatisticsStore>());
                
                return new HybridCacheStatisticsCollector(
                    localCollector,
                    distributedCollector,
                    sp.GetRequiredService<ILogger<HybridCacheStatisticsCollector>>());
            });

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

            // Register the cache manager as singleton
            services.AddSingleton<ICacheManager, CacheManager>();

            // Health checks removed per YAGNI principle

            return services;
        }

        /// <summary>
        /// Adds the cache registry for cache region management.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="autoDiscover">DEPRECATED: Auto-discovery is no longer supported. This parameter is ignored.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheRegistry(this IServiceCollection services, bool autoDiscover = false)
        {
            // Register the cache registry as singleton
            services.AddSingleton<ICacheRegistry, CacheRegistry>();

            // Auto-discovery is permanently disabled to prevent startup hangs (Issue #562)
            // All standard cache regions are pre-registered in CacheRegistry.InitializeDefaultRegions()
            // For custom regions, use configuration-based registration instead
            if (autoDiscover)
            {
                throw new NotSupportedException(
                    "Cache auto-discovery is no longer supported due to performance issues. " +
                    "All standard cache regions are automatically registered. " + 
                    "For custom regions, use configuration-based registration.");
            }

            return services;
        }

        /// <summary>
        /// Adds the complete cache infrastructure with manager and registry.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="autoDiscover">Whether to automatically discover cache regions.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheInfrastructure(
            this IServiceCollection services, 
            IConfiguration configuration,
            bool autoDiscover = false) // DISABLED: Issue #562 - causes startup hang
        {
            // Ensure memory cache is registered
            services.AddMemoryCache();

            // Configure options from configuration
            services.Configure<CacheManagerOptions>(configuration.GetSection("CacheManager"));
            services.Configure<CacheStatisticsOptions>(configuration.GetSection("CacheStatistics"));

            // Add cache registry
            services.AddCacheRegistry(autoDiscover);

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

            // Check if we have Redis configuration for statistics store
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "conduit:cache:";
                });
                services.AddSingleton<ICacheStatisticsStore, RedisCacheStatisticsStore>();
                
                // Use existing RedisConnectionFactory if available, otherwise register a lazy connection
                services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<CacheManager>>();
                    
                    // Parse connection string and set non-blocking options
                    logger.LogInformation("Creating Redis connection for cache infrastructure: {Connection}", 
                        redisConnection.Contains("password=") ? redisConnection.Replace("password=", "password=******") : redisConnection);
                    var configOptions = ConfigurationOptions.Parse(redisConnection);
                    configOptions.AbortOnConnectFail = false; // Don't block on startup
                    configOptions.ConnectTimeout = 5000; // 5 second timeout
                    configOptions.ConnectRetry = 3;
                    
                    try
                    {
                        return ConnectionMultiplexer.Connect(configOptions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create Redis connection. Cache functionality may be degraded.");
                        throw;
                    }
                });

                // Register distributed statistics collector
                services.AddSingleton<IDistributedCacheStatisticsCollector, RedisCacheStatisticsCollector>();
            }

            // Register statistics collector (hybrid if Redis is available, local otherwise)
            services.AddSingleton<ICacheStatisticsCollector>(sp =>
            {
                var distributedCollector = sp.GetService<IDistributedCacheStatisticsCollector>();
                var localCollector = new CacheStatisticsCollector(
                    sp.GetRequiredService<ILogger<CacheStatisticsCollector>>(),
                    sp.GetRequiredService<IOptions<CacheStatisticsOptions>>(),
                    sp.GetService<ICacheStatisticsStore>());
                
                if (distributedCollector != null)
                {
                    return new HybridCacheStatisticsCollector(
                        localCollector,
                        distributedCollector,
                        sp.GetRequiredService<ILogger<HybridCacheStatisticsCollector>>());
                }
                
                return localCollector;
            });

            // Register cache manager with registry integration
            services.AddSingleton<ICacheManager>(provider =>
            {
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var distributedCache = provider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                var logger = provider.GetRequiredService<ILogger<CacheManager>>();
                var options = provider.GetService<Microsoft.Extensions.Options.IOptions<CacheManagerOptions>>();
                var registry = provider.GetService<ICacheRegistry>();
                var statisticsCollector = provider.GetService<ICacheStatisticsCollector>();

                var cacheManager = new CacheManager(memoryCache, distributedCache, logger, options, statisticsCollector);

                // Defer registry sync to avoid blocking during startup
                if (registry != null)
                {
                    // Use Task.Run to sync configurations in the background
                    Task.Run(async () =>
                    {
                        try
                        {
                            foreach (var (region, config) in registry.GetAllRegions())
                            {
                                await cacheManager.UpdateRegionConfigAsync(config);
                            }
                            logger.LogInformation("Cache manager synchronized with registry");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to sync cache configurations from registry");
                        }
                    });

                    // Subscribe to registry changes (make async)
                    registry.RegionUpdated += async (sender, args) =>
                    {
                        try
                        {
                            await cacheManager.UpdateRegionConfigAsync(args.Config);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to update cache region {Region}", args.Region);
                        }
                    };
                }

                return cacheManager;
            });

            // Health checks removed per YAGNI principle

            return services;
        }

        /// <summary>
        /// Registers custom cache regions from configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Configuration section containing custom regions.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection RegisterCustomCacheRegions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<IHostedService>(provider =>
            {
                var registry = provider.GetRequiredService<ICacheRegistry>();
                var logger = provider.GetRequiredService<ILogger<CacheRegistry>>();
                
                // Register custom regions from configuration
                var customRegions = configuration.GetSection("Cache:CustomRegions");
                foreach (var region in customRegions.GetChildren())
                {
                    var config = new CacheRegionConfig
                    {
                        Region = CacheRegion.Default, // Custom regions use Default enum
                        Enabled = region.GetValue("enabled", true),
                        DefaultTTL = region.GetValue("defaultTTL", TimeSpan.FromMinutes(15)),
                        MaxTTL = region.GetValue<TimeSpan?>("maxTTL", null),
                        UseDistributedCache = region.GetValue("useDistributedCache", true),
                        UseMemoryCache = region.GetValue("useMemoryCache", true),
                        Priority = region.GetValue("priority", 50),
                        EvictionPolicy = region.GetValue("evictionPolicy", CacheEvictionPolicy.LRU),
                        MaxEntries = region.GetValue<int?>("maxEntries", null),
                        EnableDetailedStats = region.GetValue("enableDetailedStats", false)
                    };
                    
                    registry.RegisterCustomRegion(region.Key, config);
                    logger.LogInformation("Registered custom cache region '{RegionName}' from configuration", region.Key);
                }
                
                return new NoOpHostedService();
            });

            return services;
        }
        
        /// <summary>
        /// No-op hosted service for registration purposes.
        /// </summary>
        private class NoOpHostedService : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}