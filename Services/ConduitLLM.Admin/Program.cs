using System.Reflection;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Converters;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Extensions;
using ConduitLLM.Security.Middleware;

using MassTransit; // Added for event bus infrastructure


using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;
using Scalar.AspNetCore;

namespace ConduitLLM.Admin;

/// <summary>
/// Entry point for the Admin API application
/// </summary>
public partial class Program
{
    /// <summary>
    /// Application entry point that configures and starts the web application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Create a startup logger for structured logging during service registration
        using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var startupLogger = startupLoggerFactory.CreateLogger("ConduitLLM.Admin.Startup");

        // Add services to the container
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Configure JSON to use camelCase for compatibility with TypeScript clients
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

                // IMPORTANT: Make JSON deserialization case-insensitive to prevent bugs
                // This allows the API to accept both "initialBalance" and "InitialBalance"
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

                // Ensure all DateTime values serialize as UTC with 'Z' suffix
                // Fixes issue where EF Core loses DateTimeKind metadata from PostgreSQL
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
            });
        builder.Services.AddEndpointsApiExplorer();

        // Add HttpClient factory for provider connection testing
        builder.Services.AddHttpClient();

        // Configure built-in OpenAPI support
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ConduitLLM.Admin.OpenApi.AdminApiDocumentTransformer>();
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.ApiKeySecurityOperationTransformer>();
        });

        // Add leader election service for distributed background service coordination
        builder.Services.AddLeaderElection();
        startupLogger.LogInformation("Leader election service configured for background service coordination");

        // Add Core services
        builder.Services.AddCoreServices(builder.Configuration, startupLogger);

        // Add Configuration services
        builder.Services.AddConfigurationServices(builder.Configuration);

        // Add Provider services (needed for ILLMClientFactory)
        builder.Services.AddProviderServices();

        // Add Admin services
        builder.Services.AddAdminServices(builder.Configuration);

        // Configure Data Protection with Redis persistence
        var redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ResolveConnectionString();

        builder.Services.AddRedisDataProtection(redisConnectionString, "Conduit");

        // Add Redis as distributed cache for ephemeral key storage
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "conduit:";
            });
            startupLogger.LogInformation("Distributed cache configured with Redis");
        }
        else
        {
            // Fallback to in-memory cache if Redis is not configured
            builder.Services.AddDistributedMemoryCache();
            startupLogger.LogWarning("Using in-memory cache — ephemeral keys will not work across instances");
        }

        // Add SignalR with shared configuration (MessagePack, Redis backplane)
        var signalRRedisConnectionString = builder.Configuration.GetConnectionString("RedisSignalR") ?? redisConnectionString;
        builder.Services.AddConduitSignalR(
            builder.Environment,
            signalRRedisConnectionString,
            redisChannelPrefix: "conduit_admin_signalr:",
            redisDatabase: 3,
            serviceName: "ConduitLLM.Admin");

        // Configure RabbitMQ settings
        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Add media lifecycle services (scheduler, storage, distributed locking)
        builder.Services.AddMediaLifecycleServices(builder.Configuration);

        // Register MassTransit event bus for Admin API
        builder.Services.AddMassTransit(x =>
        {
            // Register consumers for Admin API cache invalidation
            x.AddConsumer<ConduitLLM.Core.Consumers.GlobalSettingCacheInvalidationHandler>();

            // Add Function Discovery Cache invalidation consumers
            x.AddConsumer<ConduitLLM.Core.Consumers.FunctionConfigurationCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Core.Consumers.FunctionDiscoveryCacheInvalidationRequestHandler>();

            // Register consumers for Admin API SignalR notifications
            // Provider health consumer removed

            if (useRabbitMq)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    // Configure RabbitMQ connection with advanced settings
                    cfg.Host(new Uri($"rabbitmq://{rabbitMqConfig.Host}:{rabbitMqConfig.Port}{rabbitMqConfig.VHost}"), h =>
                    {
                        h.Username(rabbitMqConfig.Username);
                        h.Password(rabbitMqConfig.Password);
                        h.Heartbeat(TimeSpan.FromSeconds(rabbitMqConfig.RequestedHeartbeat));
                        
                        // Publisher settings
                        h.PublisherConfirmation = rabbitMqConfig.PublisherConfirmation;
                        
                        // Advanced connection settings for publishers
                        h.RequestedChannelMax(rabbitMqConfig.ChannelMax);
                    });
                    
                    // Configure retry policy for publishing and consuming
                    cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));
                    
                    // Configure endpoints including consumers
                    cfg.ConfigureEndpoints(context);
                });
                
                startupLogger.LogInformation(
                    "Event bus configured with RabbitMQ transport (multi-instance mode) — Host: {Host}:{Port}. Publishing and consuming enabled",
                    rabbitMqConfig.Host, rabbitMqConfig.Port);
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
                    
                    // Configure endpoints
                    cfg.ConfigureEndpoints(context);
                });
                
                startupLogger.LogInformation("Event bus configured with in-memory transport (single-instance mode). Events will be processed locally");
                startupLogger.LogWarning("For production multi-instance deployments, configure RabbitMQ to ensure cross-instance cache invalidation");
            }
        });

        // Add basic health checks
        builder.Services.AddHealthChecks();
        
        // Add connection pool warmer with coordinated warming to prevent thundering herd during deployments
        // Unlike leader election, ALL instances warm their pools, but in a staggered manner
        builder.Services.AddCoordinatedConnectionPoolWarming(builder.Configuration, "AdminAPI");

        // Configure OpenTelemetry metrics and tracing
        var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var tracingEnabled = builder.Configuration.GetValue<bool>("Telemetry:TracingEnabled", true);

        var otelBuilder = builder.Services.AddOpenTelemetry()
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "ConduitLLM.Admin", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("System.Runtime")
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddPrometheusExporter();
            });

        // Add distributed tracing when enabled
        if (tracingEnabled)
        {
            otelBuilder.WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "ConduitLLM.Admin", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Filter out health check endpoints to reduce noise
                        options.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health") &&
                            !httpContext.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });
            startupLogger.LogInformation("OpenTelemetry tracing enabled — exporting to {OtlpEndpoint}", otlpEndpoint);
        }
        else
        {
            startupLogger.LogInformation("OpenTelemetry tracing disabled (set Telemetry:TracingEnabled=true to enable)");
        }

        // Add monitoring services - with leader election
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Admin.Services.AdminOperationsMetricsService>("AdminOperationsMetricsService");

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
                ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "Admin Service");
            }
        }

        // Run database migrations
        await app.RunDatabaseMigrationAsync();
        app.Logger.LogInformation("Database migrations completed successfully");

        // Seed default data (e.g., default retention policy)
        await app.SeedDefaultDataAsync();
        app.Logger.LogInformation("Default data seeding completed");

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            // Map the OpenAPI endpoint
            app.MapOpenApi("/openapi/v1.json");

            // Map Scalar UI for interactive API documentation
            app.MapScalarApiReference();

            app.Logger.LogInformation("Scalar UI available at /scalar/v1");
        }

        // Only use HTTPS redirection if explicitly enabled
        var enableHttpsRedirection = Environment.GetEnvironmentVariable("CONDUIT_ENABLE_HTTPS_REDIRECTION") != "false";
        if (enableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        // Add health endpoint authorization (early in pipeline, before authentication)
        // This protects health endpoints from external access without valid key
        app.UseHealthEndpointAuthorization();

        // Add middleware for authentication and request tracking
        app.UseAdminMiddleware();

        // Enable CORS for SignalR
        app.UseCors("AdminCorsPolicy");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        
        // Map SignalR hub with master key authentication (filter applied globally in AddSignalR)
        app.MapHub<ConduitLLM.Admin.Hubs.AdminNotificationHub>("/hubs/admin-notifications");

        // Map health check endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready") || check.Tags.Count == 0
        });

        app.Logger.LogInformation("Health check endpoints registered: /health, /health/live, /health/ready");

        // Map Prometheus metrics endpoint
        // Allow unauthenticated access from private networks (Docker internal, localhost)
        // Require authentication for external/public network requests
        app.UseOpenTelemetryPrometheusScrapingEndpoint(
            context => context.Request.Path == "/metrics" &&
                      (IpAddressHelper.IsPrivateNetworkRequest(context) ||
                       context.User.Identity?.IsAuthenticated == true));

        // For the prometheus-net library metrics
        app.UseHttpMetrics(options =>
        {
            options.ReduceStatusCodeCardinality();
            options.RequestDuration.Enabled = false; // We're using our custom middleware
            options.RequestCount.Enabled = false; // We're using our custom middleware
        });

        app.Logger.LogInformation(
            "Admin API started — Environment: {Environment}, URLs: {Urls}",
            app.Environment.EnvironmentName,
            string.Join(", ", app.Urls));

        app.Run();
    }
}

// Make Program accessible for testing
public partial class Program { }
