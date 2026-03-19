using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Security;
using ConduitLLM.Gateway.Extensions;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Providers.Extensions;
using Microsoft.Extensions.Caching.Distributed;

public partial class Program
{
    public static void ConfigureCoreServices(WebApplicationBuilder builder)
    {
        // ========== Core Infrastructure ==========

        // Add leader election service for distributed background service coordination
        builder.Services.AddLeaderElection();

        // Global settings cache service - loads settings at startup and provides fast access
        builder.Services.AddSingleton<IGlobalSettingsCacheService, GlobalSettingsCacheService>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<IGlobalSettingsCacheService>() as GlobalSettingsCacheService
            ?? throw new InvalidOperationException("GlobalSettingsCacheService must be registered as singleton"));

        // Rate Limiter registration
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy<HttpContext>("VirtualKeyPolicy", context =>
            {
                var policy = context.RequestServices.GetRequiredService<VirtualKeyRateLimitPolicy>();
                return policy.GetPartition(context);
            });
        });
        builder.Services.AddScoped<VirtualKeyRateLimitPolicy>();

        // ========== Caching Infrastructure ==========

        builder.Services.AddMemoryCache();
        builder.Services.AddCacheInfrastructure(builder.Configuration);

        // ========== Correlation Context ==========

        builder.Services.AddCorrelationContext();

        // ========== Observability ==========

        builder.Services.AddObservabilityServices(builder.Configuration);

        // ========== SignalR Reliability ==========

        builder.Services.AddSignalRReliabilityServices();

        // ========== Database Services ==========

        builder.Services.AddDatabaseServices(builder.Configuration);

        // ========== Security ==========

        builder.Services.AddCoreApiSecurity(builder.Configuration);

        // ========== HTTP Infrastructure ==========

        builder.Services.AddHttpClient();
        builder.Services.AddLLMProviderHttpClients();
        builder.Services.AddVideoGenerationHttpClients();
        builder.Services.AddHttpClientServices(builder.Configuration);

        // Register operation timeout provider for operation-aware timeout policies
        builder.Services.AddSingleton<IOperationTimeoutProvider, ConduitLLM.Core.Configuration.OperationTimeoutProvider>();

        // ========== Provider Services ==========

        // Register LLM client factory and provider services from shared extension
        builder.Services.AddProviderServices();

        // Add Provider Registry - single source of truth for provider metadata
        builder.Services.AddSingleton<IProviderMetadataRegistry, ProviderMetadataRegistry>();

        // Provider error tracking service
        builder.Services.AddSingleton<IRedisErrorStore, RedisErrorStore>();
        builder.Services.AddSingleton<IProviderErrorTrackingService, ProviderErrorTrackingService>();

        // Add performance metrics service
        builder.Services.AddSingleton<IPerformanceMetricsService, PerformanceMetricsService>();

        // ========== Billing & Pricing ==========

        builder.Services.AddBillingAndPricingServices();

        // ========== Token Management ==========

        // Parameter validation service for minimal, provider-agnostic validation
        builder.Services.AddScoped<ConduitLLM.Core.Validation.MinimalParameterValidator>();

        // Register token counter service for context management
        builder.Services.AddScoped<ITokenCounter, TiktokenCounter>();
        builder.Services.AddScoped<IContextManager, ContextManager>();

        // ========== Repositories ==========

        builder.Services.AddRepositories();

        // ========== Model Services ==========

        // Register model provider mapping service with caching decorator pattern
        builder.Services.AddScoped<ConduitLLM.Configuration.ModelProviderMappingService>(); // Inner service
        builder.Services.AddScoped<IModelProviderMappingService>(provider =>
        {
            var innerService = provider.GetRequiredService<ConduitLLM.Configuration.ModelProviderMappingService>();
            var cacheManager = provider.GetRequiredService<ICacheManager>();
            var logger = provider.GetRequiredService<ILogger<CachedModelProviderMappingService>>();
            return new CachedModelProviderMappingService(innerService, cacheManager, logger);
        });

        builder.Services.AddScoped<IProviderService, ConduitLLM.Configuration.ProviderService>();

        // Register System Notification Service
        builder.Services.AddSingleton<ISystemNotificationService, SystemNotificationService>();

        // Register Model Metadata Service
        builder.Services.AddSingleton<IModelMetadataService, ModelMetadataService>();

        // ========== Audit Services ==========

        builder.Services.AddAuditServices();

        // ========== Batch Operations ==========

        builder.Services.AddBatchOperationServices();

        // ========== Webhook Services ==========

        builder.Services.AddWebhookServices(builder.Configuration);

        // ========== Async Task Services ==========

        // Register cancellable task registry
        builder.Services.AddSingleton<ICancellableTaskRegistry, CancellableTaskRegistry>();

        // Always use hybrid database+cache task management
        builder.Services.AddScoped<IAsyncTaskService>(sp =>
        {
            var repository = sp.GetRequiredService<IAsyncTaskRepository>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            var publishEndpoint = sp.GetService<MassTransit.IPublishEndpoint>(); // Optional
            var logger = sp.GetRequiredService<ILogger<HybridAsyncTaskService>>();

            return publishEndpoint != null
                ? new HybridAsyncTaskService(repository, cache, publishEndpoint, logger)
                : new HybridAsyncTaskService(repository, cache, logger);
        });

        // ========== Conduit Service ==========

        builder.Services.AddScoped<Conduit>();

        // ========== Model Capability Services ==========

        builder.Services.AddModelCapabilityServices(builder.Configuration);

        // ========== Function Services ==========

        builder.Services.AddFunctionServices();

        // ========== Cache Services ==========

        // Register Batch Cache Invalidation service
        builder.Services.AddBatchCacheInvalidation(builder.Configuration);

        // Register Discovery Cache service for model discovery endpoint caching
        builder.Services.AddDiscoveryCache(builder.Configuration);

        // Register Discovery Cache warming as a hosted service (runs on startup)
        builder.Services.AddLeaderElectedHostedService<DiscoveryCacheWarmingService>("DiscoveryCacheWarmingService");

        // Register Function Discovery Cache service for function tool definition caching
        builder.Services.AddFunctionDiscoveryCache(builder.Configuration);

        // Register Redis batch operations for optimized cache management
        builder.Services.AddSingleton<IRedisBatchOperations, RedisBatchOperations>();

        // ========== Media Generation Services ==========

        builder.Services.AddMediaGenerationServices(builder.Configuration, builder.Environment);
    }
}
