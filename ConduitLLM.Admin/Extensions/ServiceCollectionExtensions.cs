using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Options;
using ConduitLLM.Admin.Security;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration; // For ConduitDbContext
using ConduitLLM.Core.Extensions; // For AddMediaServices extension method
using ConduitLLM.Core.Interfaces; // For IVirtualKeyCache and ILLMClientFactory
using ConduitLLM.Configuration.Interfaces; // For repository interfaces  
using ConduitLLM.Configuration.Repositories; // For repository interfaces
using ConduitLLM.Configuration.Options;

using MassTransit; // For IPublishEndpoint
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore; // For IDbContextFactory
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ConduitLLM.Admin.Extensions;

/// <summary>
/// Extension methods for configuring Admin API services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Admin API services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAdminServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure security options from environment variables
        services.ConfigureAdminSecurityOptions(configuration);

        // Register security service as singleton with factory to handle scoped dependencies
        services.AddSingleton<ISecurityService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SecurityOptions>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILogger<SecurityService>>();
            var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
            var distributedCache = serviceProvider.GetService<IDistributedCache>(); // Optional
            var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            
            return new SecurityService(options, config, logger, memoryCache, distributedCache, serviceScopeFactory);
        });

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register Ephemeral Master Key Service
        services.AddSingleton<IEphemeralMasterKeyService, EphemeralMasterKeyService>();

        // Add authentication with a custom scheme
        services.AddAuthentication("MasterKey")
            .AddScheme<MasterKeyAuthenticationSchemeOptions, MasterKeyAuthenticationHandler>("MasterKey", null);

        // Register authorization policy for master key
        services.AddSingleton<IAuthorizationHandler, MasterKeyAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            // Define the MasterKeyPolicy
            options.AddPolicy("MasterKeyPolicy", policy =>
                policy.Requirements.Add(new MasterKeyRequirement()));
            
            // Set MasterKeyPolicy as the default policy for all controllers
            // This ensures any controller with [Authorize] will use MasterKeyPolicy by default
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .AddRequirements(new MasterKeyRequirement())
                .Build();
        });

        // Register AdminVirtualKeyService with optional cache and event publishing dependencies
        services.AddScoped<IAdminVirtualKeyService>(serviceProvider =>
        {
            var virtualKeyRepository = serviceProvider.GetRequiredService<IVirtualKeyRepository>();
            var spendHistoryRepository = serviceProvider.GetRequiredService<IVirtualKeySpendHistoryRepository>();
            var groupRepository = serviceProvider.GetRequiredService<IVirtualKeyGroupRepository>();
            var cache = serviceProvider.GetService<IVirtualKeyCache>(); // Optional - null if not registered
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminVirtualKeyService>>();
            var modelProviderMappingRepository = serviceProvider.GetRequiredService<IModelProviderMappingRepository>();
            var modelCapabilityService = serviceProvider.GetRequiredService<IModelCapabilityService>();
            var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<ConduitDbContext>>();
            var mediaLifecycleService = serviceProvider.GetService<IMediaLifecycleService>(); // Optional - null if not configured
            
            return new AdminVirtualKeyService(virtualKeyRepository, spendHistoryRepository, groupRepository, cache, publishEndpoint, logger, modelProviderMappingRepository, modelCapabilityService, dbContextFactory, mediaLifecycleService);
        });
        // Register AdminModelProviderMappingService with optional event publishing dependency
        services.AddScoped<IAdminModelProviderMappingService>(serviceProvider =>
        {
            var mappingRepository = serviceProvider.GetRequiredService<IModelProviderMappingRepository>();
            var credentialRepository = serviceProvider.GetRequiredService<IProviderRepository>();
            var modelRepository = serviceProvider.GetRequiredService<IModelRepository>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminModelProviderMappingService>>();
            
            return new AdminModelProviderMappingService(mappingRepository, credentialRepository, modelRepository, publishEndpoint, logger);
        });
        services.AddScoped<IAdminRouterService, AdminRouterService>();
        
        // Register Analytics services
        services.AddSingleton<IAnalyticsMetrics, AnalyticsMetricsService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        
        // Register AdminIpFilterService with optional event publishing dependency
        services.AddScoped<IAdminIpFilterService>(serviceProvider =>
        {
            var ipFilterRepository = serviceProvider.GetRequiredService<IIpFilterRepository>();
            var ipFilterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<IpFilterOptions>>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminIpFilterService>>();
            
            return new AdminIpFilterService(ipFilterRepository, ipFilterOptions, publishEndpoint, logger);
        });
        services.AddScoped<IAdminSystemInfoService, AdminSystemInfoService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        // Register AdminGlobalSettingService with optional event publishing dependency
        services.AddScoped<IAdminGlobalSettingService>(serviceProvider =>
        {
            var globalSettingRepository = serviceProvider.GetRequiredService<IGlobalSettingRepository>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminGlobalSettingService>>();
            
            return new AdminGlobalSettingService(globalSettingRepository, publishEndpoint, logger);
        });
        // Register AdminModelCostService with optional event publishing dependency
        services.AddScoped<IAdminModelCostService>(serviceProvider =>
        {
            var modelCostRepository = serviceProvider.GetRequiredService<IModelCostRepository>();
            var requestLogRepository = serviceProvider.GetRequiredService<IRequestLogRepository>();
            var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminModelCostService>>();
            
            return new AdminModelCostService(modelCostRepository, requestLogRepository, dbContextFactory, publishEndpoint, logger);
        });

        // Register cost calculation dependencies
        services.AddScoped<ConduitLLM.Configuration.Interfaces.IModelCostService, ConduitLLM.Configuration.Services.ModelCostService>();
        services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();
        
        // Register audio-related services
        services.AddScoped<IAdminAudioProviderService, AdminAudioProviderService>();
        services.AddScoped<IAdminAudioCostService, AdminAudioCostService>();
        services.AddScoped<IAdminAudioUsageService, AdminAudioUsageService>();

        // Register media management service (requires IMediaLifecycleService to be registered)
        services.AddScoped<IAdminMediaService>(serviceProvider =>
        {
            var mediaRepository = serviceProvider.GetRequiredService<IMediaRecordRepository>();
            var mediaLifecycleService = serviceProvider.GetService<IMediaLifecycleService>();
            var logger = serviceProvider.GetRequiredService<ILogger<AdminMediaService>>();
            
            // Only register if media lifecycle service is available
            if (mediaLifecycleService == null)
            {
                throw new InvalidOperationException("IMediaLifecycleService must be registered to use AdminMediaService");
            }
            
            return new AdminMediaService(mediaRepository, mediaLifecycleService, logger);
        });

        // Register database-aware LLM client factory (must be registered before discovery service)
        services.AddScoped<ILLMClientFactory, ConduitLLM.Providers.DatabaseAwareLLMClientFactory>();

        // Configure HttpClient for discovery providers
        services.AddHttpClient("DiscoveryProviders", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-Admin/1.0");
        });

        // Model discovery providers have been removed - capabilities now come from ModelProviderMapping

        // Register Media Services using shared configuration from Core
        services.AddMediaServices(configuration);


        // Register SignalR admin notification service
        services.AddScoped<ConduitLLM.Admin.Hubs.AdminNotificationService>();

        // Register cache management service
        services.AddScoped<ICacheManagementService, CacheManagementService>();

        // Register billing audit service for comprehensive billing event tracking
        services.AddSingleton<ConduitLLM.Configuration.Interfaces.IBillingAuditService, ConduitLLM.Configuration.Services.BillingAuditService>();
        services.AddHostedService<ConduitLLM.Configuration.Services.BillingAuditService>(provider => 
            provider.GetRequiredService<ConduitLLM.Configuration.Interfaces.IBillingAuditService>() as ConduitLLM.Configuration.Services.BillingAuditService 
            ?? throw new InvalidOperationException("BillingAuditService must implement IHostedService"));

        // Register Redis error store with deferred resolution
        // IConnectionMultiplexer will be registered by AddRedisDataProtection in Program.cs after this method
        services.AddSingleton<ConduitLLM.Core.Interfaces.IRedisErrorStore>(serviceProvider =>
        {
            var redis = serviceProvider.GetService<StackExchange.Redis.IConnectionMultiplexer>();
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.RedisErrorStore>>();
            
            if (redis == null)
            {
                logger.LogError("[ConduitLLM.Admin] Redis connection not available. Redis error store will not function.");
                throw new InvalidOperationException("Redis error store requires Redis. Ensure REDIS_URL or CONDUIT_REDIS_CONNECTION_STRING is configured.");
            }
            
            logger.LogInformation("[ConduitLLM.Admin] Redis error store initialized");
            return new ConduitLLM.Core.Services.RedisErrorStore(redis, logger);
        });
        
        // Register provider error tracking service
        services.AddSingleton<ConduitLLM.Core.Interfaces.IProviderErrorTrackingService>(serviceProvider =>
        {
            var errorStore = serviceProvider.GetRequiredService<ConduitLLM.Core.Interfaces.IRedisErrorStore>();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.ProviderErrorTrackingService>>();
            
            logger.LogInformation("[ConduitLLM.Admin] Provider error tracking service initialized with Redis backend");
            return new ConduitLLM.Core.Services.ProviderErrorTrackingService(errorStore, scopeFactory, logger);
        });

        // Configure CORS for the Admin API
        services.AddCors(options =>
        {
            options.AddPolicy("AdminCorsPolicy", policy =>
            {
                var allowedOrigins = configuration.GetSection("AdminApi:AllowedOrigins").Get<string[]>();
                if (allowedOrigins != null && allowedOrigins.Length == 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
        });

        return services;
    }
}
