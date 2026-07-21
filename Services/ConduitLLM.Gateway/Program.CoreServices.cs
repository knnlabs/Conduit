using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Extensions;
using ConduitLLM.Gateway.Endpoints;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Providers.Extensions;
using Microsoft.Extensions.Caching.Distributed;

public partial class Program
{
    public static void ConfigureCoreServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ConduitLLM.Core.Services.IEventPublisher,
            ConduitLLM.Core.Services.EventPublisher>();
        builder.Services.AddGatewayEndpointHandlers();
        // ========== Core Infrastructure ==========

        // Add leader election service for distributed background service coordination
        builder.Services.AddLeaderElection();

        // Shared application services (GlobalSettingsCache, ProviderService,
        // ModelProviderMapping+decorator, ProviderMetadataRegistry)
        builder.Services.AddSharedApplicationServices();

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

        // Note: ProviderMetadataRegistry registered via AddSharedApplicationServices() above

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

        ConfigureContextManagementServices(builder);

        // ========== Repositories ==========

        builder.Services.AddRepositories();

        // ========== Model Services ==========

        // Note: ModelProviderMappingService+decorator and ProviderService registered via AddSharedApplicationServices() above

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
            var eventBus = sp.GetService<ConduitLLM.Configuration.Messaging.IEventBus>(); // Optional
            var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.HybridAsyncTaskService>>();

            return eventBus != null
                ? new ConduitLLM.Core.Services.HybridAsyncTaskService(repository, cache, eventBus, logger)
                : new ConduitLLM.Core.Services.HybridAsyncTaskService(repository, cache, logger);
        });
        builder.Services.AddHostedService<MediaTaskLeaseRecoveryService>();

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

    /// <summary>
    /// Registers the complete context-management dependency graph used by chat requests.
    /// Kept as a separate method so controller activation can be covered without booting
    /// infrastructure such as PostgreSQL, Redis, and Wolverine.
    /// </summary>
    public static void ConfigureContextManagementServices(WebApplicationBuilder builder)
    {
        builder.Services.AddConduitContextManagement(builder.Configuration);
    }
}
