using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Extensions;
using ConduitLLM.Http.Security;
using ConduitLLM.Http.Services;
using ConduitLLM.Providers;
using ConduitLLM.Providers.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Polly;
using Polly.Extensions.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Net;
using MassTransit;

public partial class Program
{
    public static void ConfigureCoreServices(WebApplicationBuilder builder)
    {
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
        builder.Services.AddScoped<IModelCostService, ConduitLLM.Configuration.Services.ModelCostService>();
        
        // Ephemeral key service for direct browser-to-API authentication (used for all direct access including SignalR)
        builder.Services.AddScoped<IEphemeralKeyService, EphemeralKeyService>();
        
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();

        // Virtual key service (Configuration layer - used by RealtimeUsageTracker)
        builder.Services.AddScoped<ConduitLLM.Configuration.Interfaces.IVirtualKeyService, ConduitLLM.Configuration.Services.VirtualKeyService>();

        builder.Services.AddMemoryCache();

        // Add cache infrastructure with distributed statistics collection
        builder.Services.AddCacheInfrastructure(builder.Configuration);

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
        builder.Services.AddSingleton<ConduitLLM.Http.Services.ISignalRAcknowledgmentService, ConduitLLM.Http.Services.SignalRAcknowledgmentService>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.SignalRAcknowledgmentService>(provider => 
            (ConduitLLM.Http.Services.SignalRAcknowledgmentService)provider.GetRequiredService<ConduitLLM.Http.Services.ISignalRAcknowledgmentService>());

        builder.Services.AddSingleton<ConduitLLM.Http.Services.ISignalRMessageQueueService, ConduitLLM.Http.Services.SignalRMessageQueueService>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.SignalRMessageQueueService>(provider => 
            (ConduitLLM.Http.Services.SignalRMessageQueueService)provider.GetRequiredService<ConduitLLM.Http.Services.ISignalRMessageQueueService>());

        builder.Services.AddSingleton<ConduitLLM.Http.Services.ISignalRConnectionMonitor, ConduitLLM.Http.Services.SignalRConnectionMonitor>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.SignalRConnectionMonitor>(provider => 
            (ConduitLLM.Http.Services.SignalRConnectionMonitor)provider.GetRequiredService<ConduitLLM.Http.Services.ISignalRConnectionMonitor>());

        builder.Services.AddSingleton<ConduitLLM.Http.Services.ISignalRMessageBatcher, ConduitLLM.Http.Services.SignalRMessageBatcher>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.SignalRMessageBatcher>(provider => 
            (ConduitLLM.Http.Services.SignalRMessageBatcher)provider.GetRequiredService<ConduitLLM.Http.Services.ISignalRMessageBatcher>());

        // Register SignalR OpenTelemetry metrics
        builder.Services.AddSingleton<ConduitLLM.Http.Metrics.SignalRMetrics>();
        builder.Services.AddHostedService<ConduitLLM.Http.Services.SignalROpenTelemetryService>();

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

        builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>(options =>
        {
            options.UseNpgsql(dbConnectionString);
            // Suppress PendingModelChangesWarning in production
            if (builder.Environment.IsProduction())
            {
                options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            }
        });
        
        // Also add scoped registration from factory for services that need direct injection
        // Note: This creates contexts from the factory on demand
        builder.Services.AddScoped<ConduitLLM.Configuration.ConduitDbContext>(provider =>
        {
            var factory = provider.GetService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
            if (factory == null)
            {
                throw new InvalidOperationException("IDbContextFactory<ConfigurationDbContext> is not registered");
            }
            return factory.CreateDbContext();
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

        // Register operation timeout provider for operation-aware timeout policies
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IOperationTimeoutProvider, ConduitLLM.Core.Configuration.OperationTimeoutProvider>();

        // Add dependencies needed for the Conduit service
        // Use DatabaseAwareLLMClientFactory to get provider credentials from database
        builder.Services.AddScoped<ILLMClientFactory, ConduitLLM.Providers.DatabaseAwareLLMClientFactory>();

        // Add Provider Registry - single source of truth for provider metadata
        builder.Services.AddSingleton<IProviderMetadataRegistry, ProviderMetadataRegistry>();
        Console.WriteLine("[ConduitLLM.Http] Provider Registry registered - centralized provider metadata management enabled");

        // Add performance metrics service
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IPerformanceMetricsService, ConduitLLM.Core.Services.PerformanceMetricsService>();

        // Image generation metrics service removed - not needed

        // Add required services for the router components
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IModelSelectionStrategy, ConduitLLM.Core.Routing.Strategies.SimpleModelSelectionStrategy>();
        builder.Services.AddScoped<ILLMRouter, ConduitLLM.Core.Routing.DefaultLLMRouter>();

        // Register token counter service for context management
        builder.Services.AddScoped<ITokenCounter, ConduitLLM.Core.Services.TiktokenCounter>();
        builder.Services.AddScoped<IContextManager, ConduitLLM.Core.Services.ContextManager>();

        // Register all repositories using the extension method
        builder.Services.AddRepositories();

        // Register services
        builder.Services.AddScoped<IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();
        builder.Services.AddScoped<IProviderService, ConduitLLM.Configuration.ProviderService>();
        builder.Services.AddScoped<IRequestLogService, ConduitLLM.Configuration.Services.RequestLogService>();

        // Register System Notification Service
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ISystemNotificationService, ConduitLLM.Http.Services.SystemNotificationService>();

        // Register Model Metadata Service
        builder.Services.AddSingleton<IModelMetadataService, ModelMetadataService>();

        // Register TaskHub Service for ITaskHub interface
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ITaskHub, ConduitLLM.Http.Services.TaskHubService>();

        // Register Batch Operation Services
        builder.Services.AddScoped<ConduitLLM.Configuration.Interfaces.IBatchOperationHistoryRepository, ConduitLLM.Configuration.Repositories.BatchOperationHistoryRepository>();
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IBatchOperationHistoryService, ConduitLLM.Http.Services.BatchOperationHistoryService>();
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IBatchOperationNotificationService, ConduitLLM.Http.Services.BatchOperationNotificationService>();
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IBatchOperationService, ConduitLLM.Core.Services.BatchOperationService>();
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IBatchSpendUpdateOperation, ConduitLLM.Core.Services.BatchOperations.BatchSpendUpdateOperation>();
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IBatchVirtualKeyUpdateOperation, ConduitLLM.Core.Services.BatchOperations.BatchVirtualKeyUpdateOperation>();
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IBatchWebhookSendOperation, ConduitLLM.Core.Services.BatchOperations.BatchWebhookSendOperation>();

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

        // Provider Discovery Service is only used in Admin API for dynamic model discovery
        // Core API relies on configured model mappings only

        // Register Video Generation Service with explicit dependencies
        builder.Services.AddScoped<IVideoGenerationService>(sp =>
        {
            var clientFactory = sp.GetRequiredService<ILLMClientFactory>();
            var capabilityService = sp.GetRequiredService<IModelCapabilityService>();
            var costService = sp.GetRequiredService<ICostCalculationService>();
            var virtualKeyService = sp.GetRequiredService<ConduitLLM.Core.Interfaces.IVirtualKeyService>();
            var mediaStorage = sp.GetRequiredService<IMediaStorageService>();
            var taskService = sp.GetRequiredService<IAsyncTaskService>();
            var logger = sp.GetRequiredService<ILogger<VideoGenerationService>>();
            var modelMappingService = sp.GetRequiredService<IModelProviderMappingService>();
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
                modelMappingService,
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

        // Register HTTP client for image downloads with retry policies
        builder.Services.AddHttpClient("ImageDownload", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60); // Timeout for large images
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-ImageDownloader/1.0");
            client.DefaultRequestHeaders.Add("Accept", "image/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
            EnableMultipleHttp2Connections = true,
            MaxResponseHeadersLength = 64 * 1024,
            ResponseDrainTimeout = TimeSpan.FromSeconds(10),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = System.Net.DecompressionMethods.All, // Handle gzip/deflate
            AllowAutoRedirect = true, // Handle redirects automatically
            MaxAutomaticRedirections = 5 // Limit redirect chains
        })
        .AddPolicyHandler(GetImageDownloadRetryPolicy())
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(120))); // Overall timeout including retries

        // Register HTTP client for video downloads with retry policies
        builder.Services.AddHttpClient("VideoDownload", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10); // Much longer timeout for large videos
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-VideoDownloader/1.0");
            client.DefaultRequestHeaders.Add("Accept", "video/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 10, // Fewer connections for large transfers
            EnableMultipleHttp2Connections = true,
            MaxResponseHeadersLength = 64 * 1024,
            ResponseDrainTimeout = TimeSpan.FromSeconds(30),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
        .AddPolicyHandler(GetVideoDownloadRetryPolicy())
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(15))); // Overall timeout including retries

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

        // Register provider model list service
        builder.Services.AddScoped<IModelListService, ModelListService>();

        // Model discovery providers have been migrated to sister classes

        // Configure HttpClient for discovery providers
        builder.Services.AddHttpClient("DiscoveryProviders", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM/1.0");
        });

        // Register provider model discovery
        builder.Services.AddScoped<IProviderModelDiscovery, ConduitLLM.Http.Services.ProviderModelDiscoveryService>();

        // Register discovery service with explicit dependency injection
        builder.Services.AddScoped<IProviderDiscoveryService>(serviceProvider =>
        {
            var clientFactory = serviceProvider.GetRequiredService<ILLMClientFactory>();
            var credentialService = serviceProvider.GetRequiredService<IProviderService>();
            var mappingService = serviceProvider.GetRequiredService<IModelProviderMappingService>();
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.ProviderDiscoveryService>>();
            var cache = serviceProvider.GetRequiredService<IMemoryCache>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var publishEndpoint = serviceProvider.GetService<MassTransit.IPublishEndpoint>(); // Optional
            var providerModelDiscovery = serviceProvider.GetRequiredService<IProviderModelDiscovery>(); // Required
            
            return new ConduitLLM.Core.Services.ProviderDiscoveryService(
                clientFactory,
                credentialService,
                mappingService,
                logger,
                cache,
                httpClientFactory,
                publishEndpoint,
                providerModelDiscovery);
        });

        // Register async task service
        // Register cancellable task registry
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.ICancellableTaskRegistry, ConduitLLM.Core.Services.CancellableTaskRegistry>();

        // Always use hybrid database+cache task management
        // This provides consistency across all deployments and proper event publishing
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAsyncTaskService>(sp =>
        {
            var repository = sp.GetRequiredService<IAsyncTaskRepository>();
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

        // Register Batch Cache Invalidation service
        builder.Services.AddBatchCacheInvalidation(builder.Configuration);

        // Register Redis batch operations for optimized cache management
        builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IRedisBatchOperations, ConduitLLM.Http.Services.RedisBatchOperations>();

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
        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAudioRouter, ConduitLLM.Core.Routing.AudioRouter>();
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
            // ImageGenerationMetricsCleanupService removed - metrics handled differently now
        }

        Console.WriteLine("[Conduit] Image generation configured with database-first architecture");
        Console.WriteLine("[Conduit] Image generation supports multi-instance deployment with lease-based task processing");
        Console.WriteLine("[Conduit] Image generation performance tracking and optimization enabled");
    }

    // Polly retry policy for image downloads with exponential backoff
    static IAsyncPolicy<HttpResponseMessage> GetImageDownloadRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles HttpRequestException and 5XX, 408 status codes
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3, // Retry up to 3 times
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2, 4, 8 seconds
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts (logger will be injected via DI in actual use)
                    var logger = context.Values.FirstOrDefault() as ILogger;
                    logger?.LogWarning("Image download retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                });
    }

    // Polly retry policy for video downloads with longer exponential backoff
    static IAsyncPolicy<HttpResponseMessage> GetVideoDownloadRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3, // Retry up to 3 times
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)), // Longer backoff: 3, 9, 27 seconds
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values.FirstOrDefault() as ILogger;
                    logger?.LogWarning("Video download retry {RetryCount} after {Delay}s", retryCount, timespan.TotalSeconds);
                });
    }

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
}