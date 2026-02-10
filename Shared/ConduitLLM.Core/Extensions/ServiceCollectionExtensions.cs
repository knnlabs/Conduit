using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Options;
using ConduitLLM.Core.Policies;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ConduitLLM.Configuration.Interfaces;

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
            
            // Register image token calculator with retry-enabled HttpClient for accurate vision model billing
            services.AddHttpClient<IImageTokenCalculator, ImageTokenCalculator>()
                .AddPolicyHandler(HttpRetryPolicies.GetStandardRetryPolicy())
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30); // Reasonable timeout for image dimension checks
                });
            
            // Register usage estimation service for streaming responses without usage data
            services.AddScoped<IUsageEstimationService, UsageEstimationService>();

            // Register context manager
            services.AddScoped<IContextManager, ContextManager>();

            return services;
        }

        /// <summary>
        /// Adds model capability detection and caching services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModelCapabilityServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register model capability service if not already registered - use database-backed implementation
            services.TryAddScoped<IModelCapabilityService, DatabaseModelCapabilityService>();

            // Register performance optimization services
            services.AddMemoryCache();

            return services;
        }

        /// <summary>
        /// Adds the ConduitLLM Batch Cache Invalidation services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddBatchCacheInvalidation(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Register configuration options
            services.Configure<BatchInvalidationOptions>(
                configuration.GetSection("CacheInvalidation"));
            
            // Register batch service as singleton and hosted service
            services.AddSingleton<BatchCacheInvalidationService>();
            services.AddSingleton<IBatchCacheInvalidationService>(provider => 
                provider.GetRequiredService<BatchCacheInvalidationService>());
            services.AddHostedService(provider => 
                provider.GetRequiredService<BatchCacheInvalidationService>());
            
            return services;
        }

        /// <summary>
        /// Adds the ConduitLLM Discovery Cache services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddDiscoveryCache(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register configuration options
            services.Configure<DiscoveryCacheOptions>(
                configuration.GetSection("Discovery"));

            // Register discovery cache service as singleton for better performance
            services.AddSingleton<IDiscoveryCacheService, DiscoveryCacheService>();

            // Ensure memory cache is registered
            services.AddMemoryCache();

            return services;
        }

        /// <summary>
        /// Adds the ConduitLLM Function Discovery Cache services to the service collection.
        /// Caches function tool definitions with per-function TTL and global enable/disable toggle.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFunctionDiscoveryCache(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register function discovery cache service as scoped (depends on scoped repositories)
            services.AddScoped<IFunctionDiscoveryCacheService, FunctionDiscoveryCacheService>();

            // Ensure memory cache is registered
            services.AddMemoryCache();

            return services;
        }

        /// <summary>
        /// Adds media storage and lifecycle services to the service collection.
        /// Shared configuration used by both Gateway API and Admin API.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMediaServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Check multiple sources for storage provider configuration
            // Priority: Configuration key > Environment variable from config > Direct environment variable
            var configProvider = configuration.GetValue<string>("ConduitLLM:Storage:Provider");
            var configEnvVar = configuration.GetValue<string>("CONDUIT_MEDIA_STORAGE_TYPE");
            var directEnvVar = Environment.GetEnvironmentVariable("CONDUIT_MEDIA_STORAGE_TYPE");
            
            var storageProvider = configProvider ?? configEnvVar ?? directEnvVar ?? "InMemory";
            
            // Log the selected storage provider for debugging (will be logged when first service is resolved)
            Console.WriteLine($"[MediaServices] Storage Provider Selected: {storageProvider}");
            
            // Configure media storage based on provider
            if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
            {
                // Configure S3StorageOptions with environment variable mapping
                services.Configure<S3StorageOptions>(options =>
                {
                    // First try to bind from the configuration section
                    configuration.GetSection(S3StorageOptions.SectionName).Bind(options);

                    // Then override with environment variables if they exist
                    ApplyConfigOrEnvVar(configuration, value => options.ServiceUrl = value,
                        "CONDUIT_S3_ENDPOINT");
                    ApplyConfigOrEnvVar(configuration, value => options.AccessKey = value,
                        "CONDUIT_S3_ACCESS_KEY_ID", "CONDUIT_S3_ACCESS_KEY");
                    ApplyConfigOrEnvVar(configuration, value => options.SecretKey = value,
                        "CONDUIT_S3_SECRET_ACCESS_KEY", "CONDUIT_S3_SECRET_KEY");
                    ApplyConfigOrEnvVar(configuration, value => options.BucketName = value,
                        "CONDUIT_S3_BUCKET_NAME");
                    ApplyConfigOrEnvVar(configuration, value => options.Region = value,
                        "CONDUIT_S3_REGION");
                    ApplyConfigOrEnvVar(configuration, value => options.PublicBaseUrl = value,
                        "CONDUIT_S3_PUBLIC_BASE_URL");

                    // Set defaults for S3 compatibility
                    options.ForcePathStyle = true;
                    options.AutoCreateBucket = true;
                });
                
                // Register S3 storage service
                services.AddSingleton<IMediaStorageService, S3MediaStorageService>();
            }
            else
            {
                // Use in-memory storage for development/testing
                services.AddSingleton<IMediaStorageService, InMemoryMediaStorageService>();
            }
            
            // Configure media management options
            services.Configure<MediaManagementOptions>(
                configuration.GetSection("ConduitLLM:MediaManagement"));
            
            // Register media lifecycle service
            services.AddScoped<IMediaLifecycleService, MediaLifecycleService>();

            // Register media lifecycle repository
            // MediaLifecycleRepository removed - consolidated into MediaRecordRepository
            // Migration: 20250827194408_ConsolidateMediaTables.cs

            return services;
        }

        /// <summary>
        /// Resolves a configuration value by checking IConfiguration keys and environment variables in order.
        /// If a non-empty value is found, applies it via the setter.
        /// </summary>
        private static void ApplyConfigOrEnvVar(IConfiguration configuration, Action<string> setter, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = configuration[key] ?? Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(value))
                {
                    setter(value);
                    return;
                }
            }
        }
    }
}
