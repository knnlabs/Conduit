using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for configuring media lifecycle services in the Admin API.
    /// </summary>
    public static class MediaLifecycleExtensions
    {
        /// <summary>
        /// Adds media lifecycle management services to the Admin API.
        /// This includes the cleanup service and related infrastructure.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMediaLifecycleServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure media lifecycle options
            services.Configure<MediaLifecycleOptions>(
                configuration.GetSection(MediaLifecycleOptions.SectionName));

            var options = configuration
                .GetSection(MediaLifecycleOptions.SectionName)
                .Get<MediaLifecycleOptions>() ?? new MediaLifecycleOptions();

            // Register distributed lock service (PostgreSQL-based, works without Redis)
            services.AddSingleton<IDistributedLockService, PostgresDistributedLockService>();

            // Register media storage service based on configuration
            RegisterMediaStorageService(services, configuration);

            // Register media deletion budget tracking service
            RegisterBudgetTrackingService(services, configuration, options);

            // Register media cleanup status service for tracking and management
            services.AddSingleton<IMediaCleanupStatusService, MediaCleanupStatusService>();

            // Register the unified cleanup service - it will check IsSchedulerEnabled internally
            // Uses distributed locking to ensure only one instance runs across a cluster
            services.AddHostedService<MediaCleanupService>();

            return services;
        }

        private static void RegisterBudgetTrackingService(
            IServiceCollection services,
            IConfiguration configuration,
            MediaLifecycleOptions options)
        {
            // Check if Redis is configured
            var redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ResolveConnectionString();

            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                // Redis is available - use Redis-based budget tracking
                // Note: IConnectionMultiplexer should already be registered by Admin API
                services.AddSingleton<IMediaDeletionBudgetService, RedisMediaDeletionBudgetService>();
            }
            else
            {
                // No Redis - use in-memory tracking (development mode)
                services.AddSingleton<IMediaDeletionBudgetService, InMemoryMediaDeletionBudgetService>();
                // stdout like the other startup warnings: stderr here fails design-time
                // hosts (build-time OpenAPI export treats stderr output as errors)
                Console.WriteLine("[ConduitLLM.Admin] WARNING: Budget tracking will not persist across restarts or be shared across instances");
            }
        }

        private static void RegisterMediaStorageService(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // Check for S3-compatible storage configuration
            var serviceUrl = configuration["CONDUIT_S3_SERVICE_URL"]
                ?? configuration["ConduitLLM:Storage:S3:ServiceUrl"]
                ?? Environment.GetEnvironmentVariable("CONDUIT_S3_SERVICE_URL");

            if (!string.IsNullOrEmpty(serviceUrl))
            {
                // Configure S3 options from environment variables
                services.Configure<ConduitLLM.Core.Options.S3StorageOptions>(options =>
                {
                    options.ServiceUrl = serviceUrl;

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

                    options.ForcePathStyle = true;
                    options.AutoCreateBucket = true;
                });

                services.AddSingleton<IMediaStorageService, S3MediaStorageService>();
            }
            else
            {
                // Use in-memory storage for development/testing
                services.AddSingleton<IMediaStorageService, InMemoryMediaStorageService>();
            }
        }
    }
}
