using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Options;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ConduitLLM.Configuration.Interfaces;
using Polly;
using Polly.Extensions.Http;
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
                .AddPolicyHandler(GetRetryPolicy())
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

            // Register capability detector if not already registered
            services.TryAddScoped<IModelCapabilityDetector, ModelCapabilityDetector>();

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
                    var endpoint = configuration["CONDUIT_S3_ENDPOINT"] ?? Environment.GetEnvironmentVariable("CONDUIT_S3_ENDPOINT");
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        options.ServiceUrl = endpoint;
                    }
                    
                    var accessKey = configuration["CONDUIT_S3_ACCESS_KEY_ID"] 
                        ?? configuration["CONDUIT_S3_ACCESS_KEY"] 
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_ACCESS_KEY_ID")
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_ACCESS_KEY");
                    if (!string.IsNullOrEmpty(accessKey))
                    {
                        options.AccessKey = accessKey;
                    }
                    
                    var secretKey = configuration["CONDUIT_S3_SECRET_ACCESS_KEY"] 
                        ?? configuration["CONDUIT_S3_SECRET_KEY"]
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_SECRET_ACCESS_KEY")
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_SECRET_KEY");
                    if (!string.IsNullOrEmpty(secretKey))
                    {
                        options.SecretKey = secretKey;
                    }
                    
                    var bucketName = configuration["CONDUIT_S3_BUCKET_NAME"] 
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_BUCKET_NAME");
                    if (!string.IsNullOrEmpty(bucketName))
                    {
                        options.BucketName = bucketName;
                    }
                    
                    var region = configuration["CONDUIT_S3_REGION"] 
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_REGION");
                    if (!string.IsNullOrEmpty(region))
                    {
                        options.Region = region;
                    }
                    
                    var publicBaseUrl = configuration["CONDUIT_S3_PUBLIC_BASE_URL"] 
                        ?? Environment.GetEnvironmentVariable("CONDUIT_S3_PUBLIC_BASE_URL");
                    if (!string.IsNullOrEmpty(publicBaseUrl))
                    {
                        options.PublicBaseUrl = publicBaseUrl;
                    }
                    
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
        /// Creates a standard retry policy for HTTP requests.
        /// Uses exponential backoff with jitter to handle transient failures.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // Handles 5xx status codes and connection failures
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + // Exponential backoff
                        TimeSpan.FromMilliseconds(new Random().Next(0, 1000)) // Jitter
                );
        }
    }
}
