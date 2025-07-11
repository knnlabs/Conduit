using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data; // Added for database initialization
using ConduitLLM.Configuration.Extensions; // Added for DataProtectionExtensions and HealthCheckExtensions
using ConduitLLM.Configuration.Repositories; // Added for repository interfaces
using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces; // Added for IVirtualKeyCache
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing; // Added for DefaultLLMClientFactory
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Adapters;
using ConduitLLM.Http.Authentication; // Added for VirtualKeyAuthenticationHandler
using ConduitLLM.Http.Controllers; // Added for RealtimeController
using ConduitLLM.Http.Extensions; // Added for AudioServiceExtensions
using ConduitLLM.Http.Middleware; // Added for Security middleware extensions
using ConduitLLM.Http.Security;
using ConduitLLM.Http.Services; // Added for ApiVirtualKeyService, RedisVirtualKeyCache, CachedApiVirtualKeyService
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.Providers.Extensions; // Add namespace for HttpClient extensions
using ConduitLLM.Admin.Services; // Added for DatabaseAwareLLMClientFactory
using ConduitLLM.Configuration.DTOs.SignalR; // Added for NotificationBatchingOptions

using MassTransit; // Added for event bus infrastructure

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Microsoft.EntityFrameworkCore.Diagnostics; // Added for warning suppression
using Microsoft.Extensions.Options; // Added for IOptions
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql.EntityFrameworkCore.PostgreSQL; // Added for PostgreSQL
using StackExchange.Redis; // Added for Redis-based task service
using Polly;
using Polly.Extensions.Http;
using Prometheus; // Added for Prometheus metrics
using OpenTelemetry.Metrics; // Added for OpenTelemetry metrics
using OpenTelemetry.Resources; // Added for ResourceBuilder extensions

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Don't load appsettings.json
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});
builder.Configuration.Sources.Clear();

// Add appsettings files for development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.HealthMonitoring.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.SecurityMonitoring.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.PerformanceMonitoring.json", optional: true, reloadOnChange: true);
}

builder.Configuration.AddEnvironmentVariables();

// Database initialization strategy
// We use a flexible approach that works for both development and production
bool skipDatabaseInit = Environment.GetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT") == "true";

if (skipDatabaseInit)
{
    Console.WriteLine("[Conduit] WARNING: Skipping database initialization. Ensure database schema is up to date.");
}
else
{
    Console.WriteLine("[Conduit] Database will be initialized automatically.");
}

// Configure JSON options for snake_case serialization (OpenAI compatibility)
var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// --- Dependency Injection Setup ---

// 1. Configure Conduit Settings
builder.Services.AddOptions<ConduitSettings>()
    .Bind(builder.Configuration.GetSection("Conduit"))
    .ValidateDataAnnotations(); // Add validation if using DataAnnotations in settings classes

// Add database-sourced settings provider to populate settings from DB
builder.Services.AddTransient<IStartupFilter, DatabaseSettingsStartupFilter>();

// Rate Limiter registration
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<Microsoft.AspNetCore.Http.HttpContext>("VirtualKeyPolicy", context =>
    {
        // Use the actual partition provider from the policy instance
        var policy = context.RequestServices.GetRequiredService<VirtualKeyRateLimitPolicy>();
        return policy.GetPartition(context);
    });
});
builder.Services.AddScoped<VirtualKeyRateLimitPolicy>();

// Model costs tracking service
builder.Services.AddScoped<ConduitLLM.Configuration.Services.IModelCostService, ConduitLLM.Configuration.Services.ModelCostService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();
builder.Services.AddMemoryCache();

// Configure OpenTelemetry with metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .SetResourceBuilder(OpenTelemetry.Resources.ResourceBuilder.CreateDefault()
                .AddService(serviceName: "ConduitLLM.Http", serviceVersion: "1.0.0"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter("ConduitLLM.SignalR") // Add SignalR metrics
            .AddPrometheusExporter();
    });

// Register monitoring services
builder.Services.AddSingleton<ConduitLLM.Http.Services.SignalRMetricsService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.SignalRMetricsService>(provider => 
    provider.GetRequiredService<ConduitLLM.Http.Services.SignalRMetricsService>());

// Register new SignalR reliability services
builder.Services.AddSingleton<ConduitLLM.Http.SignalR.Services.ISignalRAcknowledgmentService, ConduitLLM.Http.SignalR.Services.SignalRAcknowledgmentService>();
builder.Services.AddHostedService<ConduitLLM.Http.SignalR.Services.SignalRAcknowledgmentService>(provider => 
    (ConduitLLM.Http.SignalR.Services.SignalRAcknowledgmentService)provider.GetRequiredService<ConduitLLM.Http.SignalR.Services.ISignalRAcknowledgmentService>());

builder.Services.AddSingleton<ConduitLLM.Http.SignalR.Services.ISignalRMessageQueueService, ConduitLLM.Http.SignalR.Services.SignalRMessageQueueService>();
builder.Services.AddHostedService<ConduitLLM.Http.SignalR.Services.SignalRMessageQueueService>(provider => 
    (ConduitLLM.Http.SignalR.Services.SignalRMessageQueueService)provider.GetRequiredService<ConduitLLM.Http.SignalR.Services.ISignalRMessageQueueService>());

builder.Services.AddSingleton<ConduitLLM.Http.SignalR.Services.ISignalRConnectionMonitor, ConduitLLM.Http.SignalR.Services.SignalRConnectionMonitor>();
builder.Services.AddHostedService<ConduitLLM.Http.SignalR.Services.SignalRConnectionMonitor>(provider => 
    (ConduitLLM.Http.SignalR.Services.SignalRConnectionMonitor)provider.GetRequiredService<ConduitLLM.Http.SignalR.Services.ISignalRConnectionMonitor>());

builder.Services.AddSingleton<ConduitLLM.Http.SignalR.Services.ISignalRMessageBatcher, ConduitLLM.Http.SignalR.Services.SignalRMessageBatcher>();
builder.Services.AddHostedService<ConduitLLM.Http.SignalR.Services.SignalRMessageBatcher>(provider => 
    (ConduitLLM.Http.SignalR.Services.SignalRMessageBatcher)provider.GetRequiredService<ConduitLLM.Http.SignalR.Services.ISignalRMessageBatcher>());

// Register SignalR OpenTelemetry metrics
builder.Services.AddSingleton<ConduitLLM.Http.SignalR.Metrics.SignalRMetrics>();
builder.Services.AddHostedService<ConduitLLM.Http.SignalR.Services.SignalROpenTelemetryService>();

builder.Services.AddHostedService<ConduitLLM.Http.Services.InfrastructureMetricsService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.TaskProcessingMetricsService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.BusinessMetricsService>();

// 2. Register DbContext Factory (using connection string from environment variables)
var connectionStringManager = new ConduitLLM.Core.Data.ConnectionStringManager();
// Pass "CoreAPI" to get Core API-specific connection pool settings
var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("CoreAPI", msg => Console.WriteLine(msg));

// Log the connection pool settings for verification
if (dbProvider == "postgres" && dbConnectionString.Contains("MaxPoolSize"))
{
    Console.WriteLine($"[Conduit] Core API database connection pool configured:");
    var match = System.Text.RegularExpressions.Regex.Match(dbConnectionString, @"MinPoolSize=(\d+);MaxPoolSize=(\d+)");
    if (match.Success)
    {
        Console.WriteLine($"[Conduit]   Min Pool Size: {match.Groups[1].Value}");
        Console.WriteLine($"[Conduit]   Max Pool Size: {match.Groups[2].Value}");
    }
}

// Only PostgreSQL is supported
if (dbProvider != "postgres")
{
    throw new InvalidOperationException($"Only PostgreSQL is supported. Invalid provider: {dbProvider}");
}

builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
{
    options.UseNpgsql(dbConnectionString);
    // Suppress PendingModelChangesWarning in production
    if (builder.Environment.IsProduction())
    {
        options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }
});

// Authentication and authorization are configured later with policies

// Add Core API Security services
builder.Services.AddCoreApiSecurity(builder.Configuration);

// Add all the service registrations BEFORE calling builder.Build()
// Register HttpClientFactory - REQUIRED for LLMClientFactory
builder.Services.AddHttpClient();

// Add standard LLM provider HTTP clients with timeout/retry policies
builder.Services.AddLLMProviderHttpClients();

// Add video generation HTTP clients without timeout for long-running operations
builder.Services.AddVideoGenerationHttpClients();

// Register Configuration adapters early - required for DatabaseAwareLLMClientFactory
builder.Services.AddConfigurationAdapters();

// Register operation timeout provider for operation-aware timeout policies
builder.Services.AddSingleton<ConduitLLM.Core.Configuration.IOperationTimeoutProvider, ConduitLLM.Core.Configuration.OperationTimeoutProvider>();

// Add dependencies needed for the Conduit service
// Use DatabaseAwareLLMClientFactory to get provider credentials from database
builder.Services.AddScoped<ILLMClientFactory, DatabaseAwareLLMClientFactory>();
builder.Services.AddScoped<ConduitRegistry>();

// Add performance metrics service
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IPerformanceMetricsService, ConduitLLM.Core.Services.PerformanceMetricsService>();

// Add image generation metrics service
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IImageGenerationMetricsService, ConduitLLM.Core.Services.ImageGenerationMetricsService>();

// Add required services for the router components
builder.Services.AddScoped<ConduitLLM.Core.Routing.Strategies.IModelSelectionStrategy, ConduitLLM.Core.Routing.Strategies.SimpleModelSelectionStrategy>();
builder.Services.AddScoped<ILLMRouter, ConduitLLM.Core.Routing.DefaultLLMRouter>();

// Register token counter service for context management
builder.Services.AddScoped<ITokenCounter, ConduitLLM.Core.Services.TiktokenCounter>();
builder.Services.AddScoped<IContextManager, ConduitLLM.Core.Services.ContextManager>();

// Register all repositories using the extension method
builder.Services.AddRepositories();

// Register services
builder.Services.AddScoped<ConduitLLM.Configuration.IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();
builder.Services.AddScoped<ConduitLLM.Configuration.IProviderCredentialService, ConduitLLM.Configuration.ProviderCredentialService>();

// Register System Notification Service
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ISystemNotificationService, ConduitLLM.Http.Services.SystemNotificationService>();

// Register TaskHub Service for ITaskHub interface
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ITaskHub, ConduitLLM.Http.Services.TaskHubService>();

// Register Batch Operation Services
builder.Services.AddScoped<ConduitLLM.Configuration.Interfaces.IBatchOperationHistoryRepository, ConduitLLM.Configuration.Repositories.BatchOperationHistoryRepository>();
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IBatchOperationHistoryService, ConduitLLM.Http.Services.BatchOperationHistoryService>();
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IBatchOperationNotificationService, ConduitLLM.Http.Services.BatchOperationNotificationService>();
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IBatchOperationService, ConduitLLM.Core.Services.BatchOperationService>();
builder.Services.AddScoped<ConduitLLM.Core.Services.BatchOperations.BatchSpendUpdateOperation>();
builder.Services.AddScoped<ConduitLLM.Core.Services.BatchOperations.BatchVirtualKeyUpdateOperation>();
builder.Services.AddScoped<ConduitLLM.Core.Services.BatchOperations.BatchWebhookSendOperation>();

// Register Webhook Delivery Service
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IWebhookDeliveryService, ConduitLLM.Http.Services.WebhookDeliveryService>();

// Register Spend Notification Service
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ISpendNotificationService, ConduitLLM.Http.Services.SpendNotificationService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.SpendNotificationService>(sp => 
    (ConduitLLM.Http.Services.SpendNotificationService)sp.GetRequiredService<ConduitLLM.Core.Interfaces.ISpendNotificationService>());

// Register Webhook Delivery Notification Service
builder.Services.AddSingleton<ConduitLLM.Http.Services.IWebhookDeliveryNotificationService, ConduitLLM.Http.Services.WebhookDeliveryNotificationService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.WebhookDeliveryNotificationService>(sp => 
    (ConduitLLM.Http.Services.WebhookDeliveryNotificationService)sp.GetRequiredService<ConduitLLM.Http.Services.IWebhookDeliveryNotificationService>());

// Model Capability Service is registered via ServiceCollectionExtensions

// Register Video Generation Service with explicit dependencies
builder.Services.AddScoped<IVideoGenerationService>(sp =>
{
    var clientFactory = sp.GetRequiredService<ILLMClientFactory>();
    var capabilityService = sp.GetRequiredService<IModelCapabilityService>();
    var costService = sp.GetRequiredService<ICostCalculationService>();
    var virtualKeyService = sp.GetRequiredService<IVirtualKeyService>();
    var mediaStorage = sp.GetRequiredService<IMediaStorageService>();
    var taskService = sp.GetRequiredService<IAsyncTaskService>();
    var logger = sp.GetRequiredService<ILogger<VideoGenerationService>>();
    var publishEndpoint = sp.GetService<IPublishEndpoint>(); // Optional
    var taskRegistry = sp.GetService<ICancellableTaskRegistry>(); // Optional
    
    return new VideoGenerationService(
        clientFactory,
        capabilityService,
        costService,
        virtualKeyService,
        mediaStorage,
        taskService,
        logger,
        publishEndpoint,
        taskRegistry);
});

// Configure Image Generation Performance Settings
builder.Services.Configure<ConduitLLM.Core.Configuration.ImageGenerationPerformanceConfiguration>(
    builder.Configuration.GetSection("ImageGeneration:Performance"));

// Configure Video Generation Retry Settings
builder.Services.Configure<ConduitLLM.Core.Configuration.VideoGenerationRetryConfiguration>(options =>
{
    options.MaxRetries = builder.Configuration.GetValue<int>("VideoGeneration:MaxRetries", 3);
    options.BaseDelaySeconds = builder.Configuration.GetValue<int>("VideoGeneration:BaseDelaySeconds", 30);
    options.MaxDelaySeconds = builder.Configuration.GetValue<int>("VideoGeneration:MaxDelaySeconds", 3600);
    options.EnableRetries = builder.Configuration.GetValue<bool>("VideoGeneration:EnableRetries", true);
    options.RetryCheckIntervalSeconds = builder.Configuration.GetValue<int>("VideoGeneration:RetryCheckIntervalSeconds", 30);
});

// Register Webhook Notification Service with optimized configuration for high throughput
builder.Services.AddTransient<ConduitLLM.Http.Handlers.WebhookMetricsHandler>();
builder.Services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>(
    "WebhookClient", 
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10); // Reduced from 30s for better scalability
        client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
        client.DefaultRequestHeaders.ConnectionClose = false; // Keep-alive for connection reuse
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),     // Refresh connections every 5 minutes
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),  // Close idle connections after 2 minutes
        MaxConnectionsPerServer = 100,                          // Support 1000+ webhooks/min (17/sec avg, 100 concurrent)
        EnableMultipleHttp2Connections = true,                  // Allow multiple HTTP/2 connections
        MaxResponseHeadersLength = 64 * 1024,                   // 64KB for headers
        ResponseDrainTimeout = TimeSpan.FromSeconds(5),         // Drain response within 5 seconds
        ConnectTimeout = TimeSpan.FromSeconds(5),               // Connection timeout
        KeepAlivePingTimeout = TimeSpan.FromSeconds(20),        // HTTP/2 keep-alive ping timeout
        KeepAlivePingDelay = TimeSpan.FromSeconds(30)           // HTTP/2 keep-alive ping delay
    })
    .AddPolicyHandler(GetWebhookRetryPolicy())
    .AddPolicyHandler(GetWebhookCircuitBreakerPolicy())
    .AddHttpMessageHandler<ConduitLLM.Http.Handlers.WebhookMetricsHandler>();

// Polly retry policy for webhook delivery
static IAsyncPolicy<HttpResponseMessage> GetWebhookRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => !msg.IsSuccessStatusCode && msg.StatusCode != System.Net.HttpStatusCode.BadRequest)
        .WaitAndRetryAsync(
            3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2s, 4s, 8s
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                // Log retry attempts to console (logger not available in static context)
                Console.WriteLine($"[Webhook Retry] Attempt {retryCount} after {timespan.TotalMilliseconds}ms. Status: {outcome.Result?.StatusCode.ToString() ?? "N/A"}");
            });
}

// Polly circuit breaker policy for webhook delivery
static IAsyncPolicy<HttpResponseMessage> GetWebhookCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(1),
            onBreak: (result, duration) =>
            {
                // Circuit breaker opened - this will be logged by the WebhookCircuitBreaker service
                Console.WriteLine($"[Webhook Circuit Breaker] Opened for {duration.TotalSeconds} seconds");
            },
            onReset: () =>
            {
                // Circuit breaker closed
                Console.WriteLine("[Webhook Circuit Breaker] Reset");
            });
}

// Register Webhook Circuit Breaker for preventing repeated failures
builder.Services.AddMemoryCache(); // Ensure memory cache is available
builder.Services.AddSingleton<ConduitLLM.Core.Services.IWebhookCircuitBreaker>(sp =>
{
    var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.WebhookCircuitBreaker>>();
    
    // Configure circuit breaker: open after 5 failures, stay open for 5 minutes
    return new ConduitLLM.Core.Services.WebhookCircuitBreaker(
        cache, 
        logger, 
        failureThreshold: 5,
        openDuration: TimeSpan.FromMinutes(5),
        counterResetDuration: TimeSpan.FromMinutes(15));
});

// Discovery providers moved to Admin API - Core API should not handle discovery

// Virtual Key service registration will be done after Redis configuration
// Discovery service moved to Admin API - Core API should not handle discovery

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
    // Register Redis connection factory for proper connection pooling
    builder.Services.AddSingleton<ConduitLLM.Configuration.Services.RedisConnectionFactory>();
    
    // Use Redis-cached Virtual Key service for high-performance validation
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var factory = sp.GetRequiredService<ConduitLLM.Configuration.Services.RedisConnectionFactory>();
        var connectionTask = factory.GetConnectionAsync(redisConnectionString);
        return connectionTask.GetAwaiter().GetResult();
    });
    
    builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IVirtualKeyCache, RedisVirtualKeyCache>();
    
    // Register additional Redis cache services
    builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IProviderCredentialCache, RedisProviderCredentialCache>();
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
        var cache = serviceProvider.GetRequiredService<ConduitLLM.Core.Interfaces.IVirtualKeyCache>();
        var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional
        var logger = serviceProvider.GetRequiredService<ILogger<CachedApiVirtualKeyService>>();
        
        return new CachedApiVirtualKeyService(virtualKeyRepository, spendHistoryRepository, cache, publishEndpoint, logger);
    });
    
    Console.WriteLine("[Conduit] Using Redis-cached services (high-performance mode) with distributed locking");
    Console.WriteLine("[Conduit] Enabled caches: VirtualKey, ProviderCredential, GlobalSetting, ModelCost, IpFilter");
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

// Configure RabbitMQ settings
var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
    ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

// Check if RabbitMQ is configured
var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

// Register MassTransit event bus
builder.Services.AddMassTransit(x =>
{
    // Add event consumers for Core API
    x.AddConsumer<ConduitLLM.Http.EventHandlers.VirtualKeyCacheInvalidationHandler>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.SpendUpdateProcessor>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ProviderCredentialEventHandler>();
    
    // Add spend notification consumer
    x.AddConsumer<ConduitLLM.Http.EventHandlers.SpendUpdatedHandler>();
    
    // Add model capabilities consumer
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelCapabilitiesDiscoveredHandler>();
    
    // Add image generation consumers
    x.AddConsumer<ConduitLLM.Core.Services.ImageGenerationOrchestrator>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ImageGenerationProgressHandler>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ImageGenerationCompletedHandler>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ImageGenerationFailedHandler>();
    
    // Add video generation consumers
    x.AddConsumer<ConduitLLM.Core.Services.VideoGenerationOrchestrator>();
    x.AddConsumer<ConduitLLM.Core.Services.VideoProgressTrackingOrchestrator>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationProgressHandler>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationCompletedHandler>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationFailedHandler>();
    
    // Add Admin API event consumers for cache invalidation
    x.AddConsumer<ConduitLLM.Http.Consumers.GlobalSettingCacheInvalidationHandler>();
    x.AddConsumer<ConduitLLM.Http.Consumers.IpFilterCacheInvalidationHandler>();
    
    // Add async task cache invalidation handler
    x.AddConsumer<ConduitLLM.Http.EventHandlers.AsyncTaskCacheInvalidationHandler>();
    x.AddConsumer<ConduitLLM.Http.Consumers.ModelCostCacheInvalidationHandler>();
    
    // Add navigation state event consumers for real-time updates
    x.AddConsumer<ConduitLLM.Http.Consumers.ModelMappingChangedNotificationConsumer>();
    x.AddConsumer<ConduitLLM.Http.Consumers.ProviderHealthChangedNotificationConsumer>();
    x.AddConsumer<ConduitLLM.Http.Consumers.ModelCapabilitiesDiscoveredNotificationConsumer>();
    
    // Add settings refresh consumers for runtime configuration updates
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelMappingCacheInvalidationHandler>();
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ProviderCredentialCacheInvalidationHandler>();
    
    // Add media lifecycle handler for tracking generated media
    x.AddConsumer<ConduitLLM.Http.EventHandlers.MediaLifecycleHandler>();
    
    // Add video generation started handler for real-time notifications
    x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationStartedHandler>();
    
    // Add webhook delivery consumer for scalable webhook processing
    x.AddConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>();
    
    // Add model discovery notification handler for real-time model updates
    x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelDiscoveryNotificationHandler>();
    
    if (useRabbitMq)
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            // Configure RabbitMQ connection
            cfg.Host(new Uri($"rabbitmq://{rabbitMqConfig.Host}:{rabbitMqConfig.Port}{rabbitMqConfig.VHost}"), h =>
            {
                h.Username(rabbitMqConfig.Username);
                h.Password(rabbitMqConfig.Password);
                h.Heartbeat(TimeSpan.FromSeconds(rabbitMqConfig.RequestedHeartbeat));
                
                // High throughput settings
                h.PublisherConfirmation = rabbitMqConfig.PublisherConfirmation;
                
                // Advanced connection settings
                h.RequestedChannelMax(rabbitMqConfig.ChannelMax);
                h.RequestedConnectionTimeout(TimeSpan.FromSeconds(30));
            });
            
            // Configure prefetch count for consumer concurrency
            cfg.PrefetchCount = rabbitMqConfig.PrefetchCount;
            
            // Configure retry policy for reliability
            cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
            
            // Configure delayed redelivery for failed messages
            cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));
            
            // Configure webhook delivery endpoint optimized for 1000+ webhooks/minute
            cfg.ReceiveEndpoint("webhook-delivery", e =>
            {
                // Configure for high throughput - balanced for memory usage
                e.PrefetchCount = 100; // Reduced from 200 to prevent memory overload
                e.ConcurrentMessageLimit = 75; // Balanced concurrency
                
                // Use quorum queue for better reliability
                e.SetQuorumQueue();
                e.SetQueueArgument("x-delivery-limit", 10); // Max redelivery attempts
                e.SetQueueArgument("x-max-length", 50000); // Queue size limit
                e.SetQueueArgument("x-overflow", "reject-publish"); // Reject new messages when full
                
                // Configure retry with exponential backoff
                e.UseMessageRetry(r => r.Exponential(3, 
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(2)));
                
                // Circuit breaker to prevent cascading failures
                e.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                    cb.TripThreshold = 15; // 15% failure rate
                    cb.ActiveThreshold = 10; // Minimum attempts before evaluating
                    cb.ResetInterval = TimeSpan.FromMinutes(5);
                });
                
                // Rate limiting to prevent consumer overload
                e.UseRateLimit(100, TimeSpan.FromSeconds(1)); // 100 messages per second
                
                // Prevents duplicate sends during retries
                // Note: UseInMemoryOutbox is now configured at the bus level
                
                e.ConfigureConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>(context, c =>
                {
                    c.UseConcurrentMessageLimit(75);
                });
            });
            
            // Configure video generation endpoint for high throughput
            cfg.ReceiveEndpoint("video-generation-events", e =>
            {
                e.PrefetchCount = rabbitMqConfig.PrefetchCount;
                e.ConcurrentMessageLimit = rabbitMqConfig.ConcurrentMessageLimit;
                
                // Enable consume topology to properly bind consumers to the queue
                // This ensures VideoGenerationRequested events are routed to this endpoint
                e.ConfigureConsumeTopology = true;
                e.SetQuorumQueue();
                // Note: Removed x-single-active-consumer as it conflicts with partitioned processing
                // Ordering is maintained through partition keys in the event messages
                
                // Retry policy for transient failures
                e.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)));
                
                // Circuit breaker
                e.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(2);
                    cb.TripThreshold = 20; // 20% failure rate
                    cb.ActiveThreshold = 5;
                    cb.ResetInterval = TimeSpan.FromMinutes(10);
                });
                
                e.ConfigureConsumer<ConduitLLM.Core.Services.VideoGenerationOrchestrator>(context);
                e.ConfigureConsumer<ConduitLLM.Core.Services.VideoProgressTrackingOrchestrator>(context);
            });
            
            // Configure image generation endpoint
            cfg.ReceiveEndpoint("image-generation-events", e =>
            {
                e.PrefetchCount = rabbitMqConfig.PrefetchCount;
                e.ConcurrentMessageLimit = rabbitMqConfig.ConcurrentMessageLimit;
                
                e.SetQuorumQueue();
                e.SetQueueArgument("x-single-active-consumer", true);
                
                e.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)));
                
                e.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                    cb.TripThreshold = 15;
                    cb.ActiveThreshold = 5;
                    cb.ResetInterval = TimeSpan.FromMinutes(5);
                });
                
                e.ConfigureConsumer<ConduitLLM.Core.Services.ImageGenerationOrchestrator>(context);
            });
            
            // Configure spend update endpoint with strict ordering
            cfg.ReceiveEndpoint("spend-update-events", e =>
            {
                e.PrefetchCount = 10; // Lower prefetch for ordered processing
                e.ConcurrentMessageLimit = 1; // Sequential processing per partition
                
                e.SetQuorumQueue();
                e.SetQueueArgument("x-single-active-consumer", true);
                e.SetQueueArgument("x-max-length", 10000);
                
                e.UseMessageRetry(r => r.Immediate(3));
                
                e.ConfigureConsumer<ConduitLLM.Http.EventHandlers.SpendUpdateProcessor>(context);
            });
            
            // Configure dead letter exchange at the endpoint level
            // Dead letter queues are configured per endpoint above
            
            // Configure remaining endpoints with automatic topology
            cfg.ConfigureEndpoints(context);
        });
        
        Console.WriteLine($"[Conduit] Event bus configured with RabbitMQ transport (multi-instance mode) - Host: {rabbitMqConfig.Host}:{rabbitMqConfig.Port}");
        Console.WriteLine("[Conduit] Event-driven architecture ENABLED - Services will publish events for:");
        Console.WriteLine("  - Virtual Key updates (cache invalidation across instances)");
        Console.WriteLine("  - Spend updates (ordered processing with race condition prevention)");
        Console.WriteLine("  - Provider credential changes (automatic capability refresh)");
        Console.WriteLine("  - Model capability discovery (shared across all instances)");
        Console.WriteLine("  - Model mapping changes (real-time WebUI updates via SignalR)");
        Console.WriteLine("  - Provider health changes (real-time WebUI updates via SignalR)");
        Console.WriteLine("  - Global settings changes (system-wide configuration updates)");
        Console.WriteLine("  - IP filter changes (security policy updates)");
        Console.WriteLine("  - Model cost changes (pricing updates)");
        Console.WriteLine("  - Video generation tasks (partitioned processing per virtual key)");
        Console.WriteLine("  - Image generation tasks (partitioned processing per virtual key)");
    }
    else
    {
        x.UsingInMemory((context, cfg) =>
        {
            // NOTE: Using in-memory transport for single-instance deployments
            // Configure RabbitMQ environment variables for multi-instance production
            
            // Configure retry policy for reliability
            cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
            
            // Configure delayed redelivery for failed messages
            cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));
            
            // Configure webhook delivery endpoint with high throughput settings
            cfg.ReceiveEndpoint("webhook-delivery", e =>
            {
                // Configure retry with shorter intervals for webhook scenarios
                e.UseMessageRetry(r => r.Exponential(3, 
                    TimeSpan.FromSeconds(1), // Faster initial retry
                    TimeSpan.FromSeconds(30), // Max backoff
                    TimeSpan.FromSeconds(2)));
                
                // Prevents duplicate sends during retries
                // Note: UseInMemoryOutbox is now configured at the bus level
                
                e.ConfigureConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>(context, c =>
                {
                    // Configure consumer concurrency for in-memory
                    c.UseConcurrentMessageLimit(50); // Lower for single instance
                });
            });
            
            // Configure endpoints with automatic topology
            cfg.ConfigureEndpoints(context);
        });
        
        Console.WriteLine("[Conduit] Event bus configured with in-memory transport (single-instance mode)");
        Console.WriteLine("[Conduit] Event-driven architecture ENABLED - Services will publish events locally");
        Console.WriteLine("[Conduit] WARNING: For production multi-instance deployments, configure RabbitMQ:");
        Console.WriteLine("  - Set CONDUITLLM__RABBITMQ__HOST to your RabbitMQ host");
        Console.WriteLine("  - Set CONDUITLLM__RABBITMQ__USERNAME and CONDUITLLM__RABBITMQ__PASSWORD");
        Console.WriteLine("  - This enables cache consistency and ordered processing across instances");
    }
});

// Register provider model list service
builder.Services.AddScoped<ModelListService>();

// Register async task service
// Register cancellable task registry
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ICancellableTaskRegistry, ConduitLLM.Core.Services.CancellableTaskRegistry>();

// Always use hybrid database+cache task management
// This provides consistency across all deployments and proper event publishing
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAsyncTaskService>(sp =>
{
    var repository = sp.GetRequiredService<ConduitLLM.Configuration.Repositories.IAsyncTaskRepository>();
    var cache = sp.GetRequiredService<IDistributedCache>();
    var publishEndpoint = sp.GetService<MassTransit.IPublishEndpoint>(); // Optional
    var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.HybridAsyncTaskService>>();
    
    return publishEndpoint != null
        ? new ConduitLLM.Core.Services.HybridAsyncTaskService(repository, cache, publishEndpoint, logger)
        : new ConduitLLM.Core.Services.HybridAsyncTaskService(repository, cache, logger);
});

// Register Conduit service
builder.Services.AddScoped<Conduit>();

// Register File Retrieval Service
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IFileRetrievalService, ConduitLLM.Core.Services.FileRetrievalService>();

// Register Audio services
builder.Services.AddConduitAudioServices(builder.Configuration);

// Register Real-time Audio services
builder.Services.AddSingleton<IRealtimeConnectionManager, RealtimeConnectionManager>();
builder.Services.AddSingleton<IRealtimeMessageTranslatorFactory, RealtimeMessageTranslatorFactory>();
builder.Services.AddScoped<IRealtimeProxyService, RealtimeProxyService>();
builder.Services.AddScoped<IRealtimeUsageTracker, RealtimeUsageTracker>();
builder.Services.AddHostedService<RealtimeConnectionManager>(provider =>
    provider.GetRequiredService<IRealtimeConnectionManager>() as RealtimeConnectionManager ??
    throw new InvalidOperationException("RealtimeConnectionManager not registered properly"));

// Register Real-time Message Translators
builder.Services.AddSingleton<ConduitLLM.Providers.Translators.OpenAIRealtimeTranslatorV2>();
builder.Services.AddSingleton<ConduitLLM.Providers.Translators.UltravoxRealtimeTranslator>();
builder.Services.AddSingleton<ConduitLLM.Providers.Translators.ElevenLabsRealtimeTranslator>();

// Register Audio routing
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAudioRouter, ConduitLLM.Core.Routing.SimpleAudioRouter>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAudioCapabilityDetector, ConduitLLM.Core.Services.AudioCapabilityDetector>();

// Register Image Generation Retry Configuration
builder.Services.Configure<ConduitLLM.Core.Configuration.ImageGenerationRetryConfiguration>(
    builder.Configuration.GetSection("ConduitLLM:ImageGenerationRetry"));

// Add background services for monitoring and cleanup (skip in test environment to prevent endless loops)
if (builder.Environment.EnvironmentName != "Test")
{
    // Add database-based background service for image generation
    // REMOVED: ImageGenerationDatabaseBackgroundService - Events are now processed by ImageGenerationOrchestrator consumer

    // DISABLED: VideoGenerationBackgroundService causes duplicate event publishing
    // The VideoGenerationService already publishes VideoGenerationRequested events directly
    // builder.Services.AddHostedService<VideoGenerationBackgroundService>();

    // Add background service for image generation metrics cleanup
    builder.Services.AddHostedService<ImageGenerationMetricsCleanupService>();
}

Console.WriteLine("[Conduit] Image generation configured with database-first architecture");
Console.WriteLine("[Conduit] Image generation supports multi-instance deployment with lease-based task processing");
Console.WriteLine("[Conduit] Image generation performance tracking and optimization enabled");

// Register Media Storage Service
var storageProvider = builder.Configuration.GetValue<string>("ConduitLLM:Storage:Provider") ?? "InMemory";
Console.WriteLine($"[Conduit] Storage Provider: {storageProvider}");
if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[Conduit] Configuring S3 Media Storage Service");
    builder.Services.Configure<ConduitLLM.Core.Options.S3StorageOptions>(
        builder.Configuration.GetSection(ConduitLLM.Core.Options.S3StorageOptions.SectionName));
    builder.Services.AddSingleton<IMediaStorageService, S3MediaStorageService>();
}
else
{
    Console.WriteLine("[Conduit] Using In-Memory Media Storage (development mode)");
    // Use in-memory storage for development
    builder.Services.AddSingleton<IMediaStorageService>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<InMemoryMediaStorageService>>();
        
        // Try to get the public base URL from configuration
        var mediaBaseUrl = builder.Configuration["CONDUITLLM:MEDIA_BASE_URL"] 
            ?? builder.Configuration["Media:BaseUrl"]
            ?? builder.Configuration["CONDUIT_MEDIA_BASE_URL"]
            ?? Environment.GetEnvironmentVariable("CONDUITLLM__MEDIA_BASE_URL");
            
        // If not configured, try to determine from environment
        if (string.IsNullOrEmpty(mediaBaseUrl))
        {
            var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000";
            var firstUrl = urls.Split(';').First();
            
            // Replace wildcard bindings with localhost for media URLs
            if (firstUrl.Contains("+:") || firstUrl.Contains("*:"))
            {
                var port = firstUrl.Split(':').Last();
                mediaBaseUrl = $"http://localhost:{port}";
            }
            else
            {
                mediaBaseUrl = firstUrl;
            }
        }
        
        logger.LogInformation("Media storage base URL configured as: {BaseUrl}", mediaBaseUrl);
        return new InMemoryMediaStorageService(logger, mediaBaseUrl);
    });
}

// Register Media Lifecycle Management Services
builder.Services.Configure<ConduitLLM.Core.Services.MediaManagementOptions>(
    builder.Configuration.GetSection("ConduitLLM:MediaManagement"));

builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IMediaLifecycleService, ConduitLLM.Core.Services.MediaLifecycleService>();

// Add media maintenance background service
builder.Services.AddHostedService<ConduitLLM.Http.Services.MediaMaintenanceBackgroundService>();

// Add CORS support for WebUI requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5001",  // WebUI access
                "http://webui:8080",      // Docker internal
                "http://localhost:8080",  // Alternative local access
                "http://127.0.0.1:5001"   // Alternative localhost format
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // Enable credentials for auth headers
    });
});

// Add Authentication and Authorization
builder.Services.AddAuthentication("VirtualKey")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, VirtualKeyAuthenticationHandler>(
        "VirtualKey", options => { });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("VirtualKey")
        .RequireAuthenticatedUser()
        .Build();
    
    // Allow endpoints to opt out of authentication with [AllowAnonymous]
    options.FallbackPolicy = null;
    
    // Add policy for SignalR hubs requiring virtual key authentication
    options.AddPolicy("RequireVirtualKey", policy =>
    {
        policy.AuthenticationSchemes.Add("VirtualKey");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("VirtualKeyId");
    });
    
    // Add policy for SignalR hubs requiring admin privileges
    options.AddPolicy("RequireAdminVirtualKey", policy =>
    {
        policy.AuthenticationSchemes.Add("VirtualKey");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("VirtualKeyId");
        policy.RequireClaim("IsAdmin", "true");
    });
    
    // Add policy for admin-only access (e.g., metrics dashboard)
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.AuthenticationSchemes.Add("VirtualKey");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("IsAdmin", "true");
    });
});

// Add Controller support
builder.Services.AddControllers();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Conduit Core API",
        Version = "v1",
        Description = "OpenAI-compatible API for multi-provider LLM access"
    });
    
    // Add API Key authentication
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Virtual Key authentication using Authorization header",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Register VirtualKeyHubFilter for SignalR authentication
builder.Services.AddSingleton<ConduitLLM.Http.Authentication.VirtualKeyHubFilter>();

// Register rate limit cache service for SignalR
builder.Services.AddSingleton<ConduitLLM.Http.Services.VirtualKeyRateLimitCache>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.VirtualKeyRateLimitCache>(provider => 
    provider.GetRequiredService<ConduitLLM.Http.Services.VirtualKeyRateLimitCache>());

// Register SignalR rate limit filter
builder.Services.AddSingleton<ConduitLLM.Http.Authentication.VirtualKeySignalRRateLimitFilter>();

// Register SignalR metrics
builder.Services.AddSingleton<ConduitLLM.Http.Metrics.SignalRMetrics>();
builder.Services.AddSingleton<ConduitLLM.Http.Metrics.ISignalRMetrics>(sp => sp.GetRequiredService<ConduitLLM.Http.Metrics.SignalRMetrics>());

// Register SignalR metrics filter
builder.Services.AddSingleton<ConduitLLM.Http.Filters.SignalRMetricsFilter>();

// Register SignalR error handling filter
builder.Services.AddSingleton<ConduitLLM.Http.Filters.SignalRErrorHandlingFilter>();

// Register SignalR authentication service
builder.Services.AddScoped<ConduitLLM.Http.Authentication.ISignalRAuthenticationService, ConduitLLM.Http.Authentication.SignalRAuthenticationService>();

// Register Metrics Aggregation Service and Hub
builder.Services.AddSingleton<ConduitLLM.Http.Hubs.IMetricsAggregationService, ConduitLLM.Http.Services.MetricsAggregationService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.MetricsAggregationService>(sp => 
    (ConduitLLM.Http.Services.MetricsAggregationService)sp.GetRequiredService<ConduitLLM.Http.Hubs.IMetricsAggregationService>());

// Register Infrastructure and Business Metrics Background Services
builder.Services.AddHostedService<ConduitLLM.Http.Services.InfrastructureMetricsService>();
builder.Services.AddHostedService<ConduitLLM.Http.Services.BusinessMetricsService>();

// Add SignalR for real-time navigation state updates
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.StreamBufferCapacity = 10;
    
    // Add global filters
    options.AddFilter<ConduitLLM.Http.Filters.SignalRMetricsFilter>();
    options.AddFilter<ConduitLLM.Http.Filters.SignalRErrorHandlingFilter>();
    options.AddFilter<ConduitLLM.Http.Authentication.VirtualKeyHubFilter>();
    options.AddFilter<ConduitLLM.Http.Authentication.VirtualKeySignalRRateLimitFilter>();
});

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

// Register navigation state notification service
builder.Services.AddSingleton<INavigationStateNotificationService, NavigationStateNotificationService>();

// Register settings refresh service for runtime configuration updates
builder.Services.AddSingleton<ISettingsRefreshService, SettingsRefreshService>();

// Register media lifecycle repository
builder.Services.AddScoped<IMediaLifecycleRepository, MediaLifecycleRepository>();

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

// Register model discovery notification services
builder.Services.Configure<NotificationBatchingOptions>(builder.Configuration.GetSection("ConduitLLM:NotificationBatching"));
builder.Services.AddSingleton<IModelDiscoverySubscriptionManager, ModelDiscoverySubscriptionManager>();
builder.Services.AddSingleton<INotificationSeverityClassifier, NotificationSeverityClassifier>();
builder.Services.AddSingleton<IModelDiscoveryNotificationBatcher, ModelDiscoveryNotificationBatcher>();
builder.Services.AddHostedService<ModelDiscoveryNotificationBatcher>(sp => 
    (ModelDiscoveryNotificationBatcher)sp.GetRequiredService<IModelDiscoveryNotificationBatcher>());

// Register batch spend update service for optimized Virtual Key operations
builder.Services.AddSingleton<ConduitLLM.Configuration.Services.BatchSpendUpdateService>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Configuration.Services.BatchSpendUpdateService>>();
    var batchService = new ConduitLLM.Configuration.Services.BatchSpendUpdateService(serviceProvider, logger);
    
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
builder.Services.AddHostedService<ConduitLLM.Configuration.Services.BatchSpendUpdateService>(serviceProvider =>
    serviceProvider.GetRequiredService<ConduitLLM.Configuration.Services.BatchSpendUpdateService>());

// Register batch webhook publisher for high-throughput webhook delivery
if (useRabbitMq)
{
    builder.Services.AddBatchWebhookPublisher(options =>
    {
        options.MaxBatchSize = 100;
        options.MaxBatchDelay = TimeSpan.FromMilliseconds(100);
        options.ConcurrentPublishers = 3;
    });
    Console.WriteLine("[Conduit] Batch webhook publisher configured for high-throughput delivery");
}

// Add standardized health checks (skip in test environment to avoid conflicts)
if (builder.Environment.EnvironmentName != "Test")
{
    // Use the same Redis connection string we configured above for health checks
    var healthChecksBuilder = builder.Services.AddConduitHealthChecks(dbConnectionString, redisConnectionString, true, rabbitMqConfig);

    // Add comprehensive RabbitMQ health check if RabbitMQ is configured
    if (useRabbitMq)
    {
        healthChecksBuilder.AddCheck<ConduitLLM.Core.HealthChecks.RabbitMQHealthCheck>(
            "rabbitmq_comprehensive",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: new[] { "messaging", "rabbitmq", "ready", "performance" });
    }

    // Add audio-specific health checks if audio services are configured
    if (builder.Configuration.GetSection("AudioService:Providers").Exists())
    {
        healthChecksBuilder.AddAudioHealthChecks(builder.Configuration);
    }
    
    // Add advanced health monitoring checks (includes SignalR and HTTP connection pool checks)
    healthChecksBuilder.AddAdvancedHealthMonitoring(builder.Configuration);
}

// Add health monitoring services
builder.Services.AddHealthMonitoring(builder.Configuration);

// Add database initialization services
builder.Services.AddScoped<ConduitLLM.Configuration.Data.DatabaseInitializer>();

// Add connection pool warmer for better startup performance
builder.Services.AddHostedService<ConduitLLM.Core.Services.ConnectionPoolWarmer>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.ConnectionPoolWarmer>>();
    return new ConduitLLM.Core.Services.ConnectionPoolWarmer(serviceProvider, logger, "CoreAPI");
});

var app = builder.Build();

// Log deprecation warnings and validate Redis URL
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    ConduitLLM.Configuration.Extensions.DeprecationWarnings.LogEnvironmentVariableDeprecations(logger);
    
    // Validate Redis URL if provided
    var envRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
    if (!string.IsNullOrEmpty(envRedisUrl))
    {
        ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "Http Service");
    }
}

// Initialize database - Always run unless explicitly told to skip
// This ensures users get automatic schema updates when pulling new versions
if (!skipDatabaseInit)
{
    using (var scope = app.Services.CreateScope())
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.Data.DatabaseInitializer>();
        var initLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            initLogger.LogInformation("Starting database initialization...");

            // Wait for database to be available (especially important in Docker)
            var maxRetries = 10;
            var retryDelay = 3000; // 3 seconds between retries

            var success = await dbInitializer.InitializeDatabaseAsync(maxRetries, retryDelay);

            if (success)
            {
                initLogger.LogInformation("Database initialization completed successfully");
            }
            else
            {
                initLogger.LogError("Database initialization failed after {MaxRetries} attempts", maxRetries);
                // Always fail hard if database initialization fails
                // This prevents running with an incomplete schema
                throw new InvalidOperationException($"Database initialization failed after {maxRetries} attempts. Please check database connectivity and logs.");
            }
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex, "Critical error during database initialization");
            // Re-throw to prevent the application from starting with a broken database
            throw new InvalidOperationException("Failed to initialize database. Application cannot start.", ex);
        }
    }
}
else
{
    using (var scope = app.Services.CreateScope())
    {
        var initLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        initLogger.LogWarning("Database initialization is skipped. Ensure database schema is up to date.");
    }
}

// Enable CORS
app.UseCors();

// Enable Swagger UI in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Conduit Core API v1");
        c.RoutePrefix = "swagger";
    });
}

// Add security headers
app.UseCoreApiSecurityHeaders();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Note: VirtualKeyAuthenticationHandler is now used instead of middleware
// The authentication handler is registered with the "VirtualKey" scheme above

// Add HTTP metrics middleware for comprehensive request tracking
app.UseMiddleware<ConduitLLM.Http.Middleware.HttpMetricsMiddleware>();

// Add security middleware (IP filtering, rate limiting, ban checks)
app.UseCoreApiSecurity();

// Enable rate limiting (now that Virtual Keys are authenticated)
app.UseRateLimiter();

// Add timeout diagnostics middleware
app.UseMiddleware<ConduitLLM.Core.Middleware.TimeoutDiagnosticsMiddleware>();

// Enable WebSockets for real-time communication
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
});

// Add controllers to the app
app.MapControllers();
Console.WriteLine("[Conduit API] Controllers registered");

// Map SignalR hubs for real-time updates

// Customer-facing hubs require virtual key authentication
app.MapHub<ConduitLLM.Http.Hubs.VideoGenerationHub>("/hubs/video-generation")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR VideoGenerationHub registered at /hubs/video-generation (requires authentication)");

app.MapHub<ConduitLLM.Http.Hubs.ImageGenerationHub>("/hubs/image-generation")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR ImageGenerationHub registered at /hubs/image-generation (requires authentication)");

app.MapHub<ConduitLLM.Http.Hubs.TaskHub>("/hubs/tasks")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR TaskHub registered at /hubs/tasks (requires authentication)");

app.MapHub<ConduitLLM.Http.Hubs.SystemNotificationHub>("/hubs/notifications")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR SystemNotificationHub registered at /hubs/notifications (requires authentication)");

app.MapHub<ConduitLLM.Http.Hubs.SpendNotificationHub>("/hubs/spend")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR SpendNotificationHub registered at /hubs/spend (requires authentication)");

app.MapHub<ConduitLLM.Http.Hubs.WebhookDeliveryHub>("/hubs/webhooks")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR WebhookDeliveryHub registered at /hubs/webhooks (requires authentication)");

app.MapHub<ConduitLLM.Http.Hubs.ModelDiscoveryHub>("/hubs/model-discovery")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR ModelDiscoveryHub registered at /hubs/model-discovery (requires authentication)");

// Admin-only hub for metrics dashboard
app.MapHub<ConduitLLM.Http.Hubs.MetricsHub>("/hubs/metrics")
    .RequireAuthorization("AdminOnly");
Console.WriteLine("[Conduit API] SignalR MetricsHub registered at /hubs/metrics (requires admin authentication)");

// Admin-only hub for health monitoring
app.MapHub<ConduitLLM.Http.Hubs.HealthMonitoringHub>("/hubs/health-monitoring")
    .RequireAuthorization("AdminOnly");
Console.WriteLine("[Conduit API] SignalR HealthMonitoringHub registered at /hubs/health-monitoring (requires admin authentication)");

// Admin-only hub for security monitoring
app.MapHub<ConduitLLM.Http.Hubs.SecurityMonitoringHub>("/hubs/security-monitoring")
    .RequireAuthorization("AdminOnly");
Console.WriteLine("[Conduit API] SignalR SecurityMonitoringHub registered at /hubs/security-monitoring (requires admin authentication)");

// Virtual key management hub for real-time key management updates
app.MapHub<ConduitLLM.Http.Hubs.VirtualKeyManagementHub>("/hubs/virtual-key-management")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR VirtualKeyManagementHub registered at /hubs/virtual-key-management (requires authentication)");

// Usage analytics hub for real-time analytics and monitoring
app.MapHub<ConduitLLM.Http.Hubs.UsageAnalyticsHub>("/hubs/usage-analytics")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR UsageAnalyticsHub registered at /hubs/usage-analytics (requires authentication)");

// Enhanced video generation hub with acknowledgment support
app.MapHub<ConduitLLM.Http.SignalR.Hubs.EnhancedVideoGenerationHub>("/hubs/enhanced-video-generation")
    .RequireAuthorization();
Console.WriteLine("[Conduit API] SignalR EnhancedVideoGenerationHub registered at /hubs/enhanced-video-generation (requires authentication)");

// Map health check endpoints without authentication requirement
// Health endpoints should be accessible without authentication for monitoring tools
app.MapSecureConduitHealthChecks(requireAuthorization: false);

// Map Prometheus metrics endpoint for scraping
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
Console.WriteLine("[Conduit API] Prometheus metrics endpoint registered at /metrics");

// Add completions endpoint (legacy)
app.MapPost("/v1/completions", ([FromServices] ILogger<Program> logger) =>
{
    logger.LogInformation("Legacy /completions endpoint called.");
    return Results.Json(
        new
        {
            error = "The /completions endpoint is not implemented. Please use /chat/completions."
        },
        statusCode: 501,
        options: jsonSerializerOptions
    );
});

// Add embeddings endpoint
app.MapPost("/v1/embeddings", async (
    [FromBody] EmbeddingRequest? request,
    [FromServices] ILLMRouter router,
    [FromServices] ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request == null)
    {
        return Results.BadRequest(new { error = "Invalid request body." });
    }

    try
    {
        logger.LogInformation("Processing embeddings request for model: {Model}", request.Model);
        var response = await router.CreateEmbeddingAsync(request, cancellationToken: cancellationToken);
        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing embeddings request for model: {Model}", request.Model);
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

// Add models endpoint
app.MapGet("/v1/models", ([FromServices] ILLMRouter router, [FromServices] ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Getting available models");

        // Get model names from the router
        var modelNames = router.GetAvailableModels();

        // Convert to OpenAI format
        var basicModelData = modelNames.Select(m => new
        {
            id = m,
            @object = "model"
        }).ToList();

        // Create the response envelope
        var response = new
        {
            data = basicModelData,
            @object = "list"
        };

        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving models list");
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

// Add chat completions endpoint
app.MapPost("/v1/chat/completions", async (
    [FromBody] ChatCompletionRequest request,
    [FromServices] Conduit conduit,
    [FromServices] ILogger<Program> logger,
    HttpRequest httpRequest) =>
{
    logger.LogInformation("Received /v1/chat/completions request for model: {Model}", request.Model);

    try
    {
        // Non-streaming path
        if (request.Stream != true)
        {
            logger.LogInformation("Handling non-streaming request.");
            var response = await conduit.CreateChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted);
            return Results.Json(response, options: jsonSerializerOptions);
        }
        else
        {
            logger.LogInformation("Handling streaming request.");
            
            // Use enhanced SSE writer for performance metrics support
            var response = httpRequest.HttpContext.Response;
            var sseWriter = response.CreateEnhancedSSEWriter(jsonSerializerOptions);
            
            // Create metrics collector if performance tracking is enabled
            var settings = httpRequest.HttpContext.RequestServices.GetRequiredService<IOptions<ConduitSettings>>().Value;
            StreamingMetricsCollector? metricsCollector = null;
            
            if (settings.PerformanceTracking?.Enabled == true && settings.PerformanceTracking.TrackStreamingMetrics)
            {
                logger.LogInformation("Performance tracking enabled for streaming request");
                var requestId = Guid.NewGuid().ToString();
                response.Headers["X-Request-ID"] = requestId;
                
                // Get provider info for metrics from settings
                var modelMapping = settings.ModelMappings?.FirstOrDefault(m => 
                    string.Equals(m.ModelAlias, request.Model, StringComparison.OrdinalIgnoreCase));
                var providerName = modelMapping?.ProviderName ?? "unknown";
                
                logger.LogInformation("Creating StreamingMetricsCollector for model {Model}, provider {Provider}", request.Model, providerName);
                metricsCollector = new StreamingMetricsCollector(
                    requestId,
                    request.Model,
                    providerName);
            }
            else
            {
                logger.LogInformation("Performance tracking disabled for streaming request. Enabled: {Enabled}, TrackStreaming: {TrackStreaming}", 
                    settings.PerformanceTracking?.Enabled, 
                    settings.PerformanceTracking?.TrackStreamingMetrics);
            }

            try
            {
                await foreach (var chunk in conduit.StreamChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted))
                {
                    // Write content event
                    await sseWriter.WriteContentEventAsync(chunk);
                    
                    // Track metrics if enabled
                    if (metricsCollector != null && chunk?.Choices?.Count > 0)
                    {
                        var hasContent = chunk.Choices.Any(c => !string.IsNullOrEmpty(c.Delta?.Content));
                        if (hasContent)
                        {
                            if (metricsCollector.GetMetrics().TimeToFirstTokenMs == null)
                            {
                                metricsCollector.RecordFirstToken();
                            }
                            else
                            {
                                metricsCollector.RecordToken();
                            }
                        }
                        
                        // Emit metrics periodically
                        if (metricsCollector.ShouldEmitMetrics())
                        {
                            logger.LogDebug("Emitting streaming metrics");
                            await sseWriter.WriteMetricsEventAsync(metricsCollector.GetMetrics());
                        }
                    }
                }

                // Write final metrics if tracking is enabled
                if (metricsCollector != null)
                {
                    var finalMetrics = metricsCollector.GetFinalMetrics();
                    await sseWriter.WriteFinalMetricsEventAsync(finalMetrics);
                }

                // Write [DONE] to signal the end of the stream
                await sseWriter.WriteDoneEventAsync();
            }
            catch (Exception streamEx)
            {
                logger.LogError(streamEx, "Error in stream processing");
                await sseWriter.WriteErrorEventAsync(streamEx.Message);
            }

            return Results.Empty;
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing request");
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

app.Run();

// Helper class for OpenAI-compatible error response
public class OpenAIErrorResponse
{
    [JsonPropertyName("error")]
    public required OpenAIError Error { get; set; }
}

public class OpenAIError
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("param")]
    public string? Param { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

// Helper for triggering database settings load on startup
public class DatabaseSettingsStartupFilter : IStartupFilter
{
    // Inject both factories
    private readonly IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> _configDbContextFactory;
    private readonly IOptions<ConduitSettings> _settingsOptions;
    private readonly ILogger<DatabaseSettingsStartupFilter> _logger;

    public DatabaseSettingsStartupFilter(
        IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configDbContextFactory, // Inject correct factory
        IOptions<ConduitSettings> settingsOptions,
        ILogger<DatabaseSettingsStartupFilter> logger)
    {
        _configDbContextFactory = configDbContextFactory; // Assign correct factory
        _settingsOptions = settingsOptions;
        _logger = logger;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        LoadSettingsFromDatabaseAsync().GetAwaiter().GetResult(); // Load synchronously during startup
        return next;
    }

    private async Task LoadSettingsFromDatabaseAsync()
    {
        _logger.LogInformation("Attempting to load settings from database on startup...");
        var settings = _settingsOptions.Value;
        try
        {
            // Load provider credentials from Config context
            await using var configDbContext = await _configDbContextFactory.CreateDbContextAsync();
            var providerCredsList = await configDbContext.ProviderCredentials.ToListAsync();
            if (providerCredsList.Any())
            {
                _logger.LogInformation("Found {Count} provider credentials in database", providerCredsList.Count);

                // Convert database provider credentials to Core provider credentials
                var providersList = providerCredsList.Select(p => new ProviderCredentials
                {
                    ProviderName = p.ProviderName,
                    ApiKey = p.ApiKey,
                    ApiVersion = p.ApiVersion,
                    ApiBase = p.BaseUrl // Map BaseUrl from DB entity to ApiBase in settings entity
                }).ToList();

                // Now integrate these with existing settings
                // Two approaches: 
                // 1. Replace in-memory with DB values
                // 2. Merge DB with in-memory (with DB taking precedence)
                // Using approach #2 here

                if (settings.ProviderCredentials == null)
                {
                    settings.ProviderCredentials = new List<ProviderCredentials>();
                }

                // Remove any in-memory providers that exist in DB to avoid duplicates
                settings.ProviderCredentials.RemoveAll(p =>
                    providersList.Any(dbp =>
                        string.Equals(dbp.ProviderName, p.ProviderName, StringComparison.OrdinalIgnoreCase)));

                // Then add all the database credentials
                settings.ProviderCredentials.AddRange(providersList);

                foreach (var cred in providersList)
                {
                    _logger.LogInformation("Loaded credentials for provider: {ProviderName}", cred.ProviderName);
                }
            }
            else
            {
                _logger.LogWarning("No provider credentials found in database");
            }

            // Load model mappings using ModelProviderMappingRepository directly
            var modelMappingsEntities = await configDbContext.ModelProviderMappings
                .Include(m => m.ProviderCredential)
                .ToListAsync();

            if (modelMappingsEntities.Any())
            {
                _logger.LogInformation("Found {Count} model mappings in database", modelMappingsEntities.Count);

                // Convert database model mappings to Core model mappings
                var modelMappingsList = modelMappingsEntities.Select(m => new ModelProviderMapping
                {
                    ModelAlias = m.ModelAlias,
                    ProviderName = m.ProviderCredential.ProviderName,
                    ProviderModelId = m.ProviderModelName
                }).ToList();

                // Configure the model mappings in settings
                if (settings.ModelMappings == null)
                {
                    settings.ModelMappings = new List<ModelProviderMapping>();
                }

                // Remove existing mappings that exist in DB to avoid duplicates
                settings.ModelMappings.RemoveAll(m =>
                    modelMappingsList.Any(dbm =>
                        string.Equals(dbm.ModelAlias, m.ModelAlias, StringComparison.OrdinalIgnoreCase)));

                // Add all the database model mappings
                settings.ModelMappings.AddRange(modelMappingsList);

                foreach (var mapping in modelMappingsList)
                {
                    _logger.LogInformation("Loaded model mapping: {ModelAlias} -> {ProviderName}/{ProviderModelId}",
                        mapping.ModelAlias, mapping.ProviderName, mapping.ProviderModelId);
                }
            }
            else
            {
                _logger.LogWarning("No model mappings found in database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings from database");
        }
    }
}

// Make Program class accessible for testing
public partial class Program { }
