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
        /// Automatically detects Redis configuration and uses distributed statistics if available.
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

            // Check if Redis is configured for the distributed cache tier
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                // Add Redis distributed cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "conduit:cache:";
                });

                // Register Redis connection multiplexer with lazy initialization
                services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<CacheManager>>();
                    logger.LogInformation("Creating Redis connection for the distributed cache tier");

                    var configOptions = ConfigurationOptions.Parse(redisConnection);
                    configOptions.AbortOnConnectFail = false;
                    configOptions.ConnectTimeout = 5000;
                    configOptions.ConnectRetry = 3;

                    try
                    {
                        return ConnectionMultiplexer.Connect(configOptions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create Redis connection. Cache will fall back to memory only.");
                        throw;
                    }
                });
            }

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

            // Add cache registry
            services.AddCacheRegistry(autoDiscover);

            // Register policy engine
            services.AddSingleton<ICachePolicyEngine, CachePolicyEngine>();

            // Check if we have Redis configuration for the distributed cache tier
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "conduit:cache:";
                });

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
            }

            // Register cache manager with registry integration
            services.AddSingleton<ICacheManager>(provider =>
            {
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var distributedCache = provider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                var logger = provider.GetRequiredService<ILogger<CacheManager>>();
                var options = provider.GetService<Microsoft.Extensions.Options.IOptions<CacheManagerOptions>>();
                var registry = provider.GetService<ICacheRegistry>();

                var cacheManager = new CacheManager(memoryCache, distributedCache, logger, options);

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