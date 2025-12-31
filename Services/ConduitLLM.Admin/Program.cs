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
        Console.WriteLine("[ConduitLLM.Admin] Leader election service configured for background service coordination");

        // Add Core services
        builder.Services.AddCoreServices(builder.Configuration);

        // Add Configuration services
        builder.Services.AddConfigurationServices(builder.Configuration);

        // Add Provider services (needed for ILLMClientFactory)
        builder.Services.AddProviderServices();

        // Add Admin services
        builder.Services.AddAdminServices(builder.Configuration);

        // Configure Data Protection with Redis persistence
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
            }
        }

        builder.Services.AddRedisDataProtection(redisConnectionString, "Conduit");

        // Add Redis as distributed cache for ephemeral key storage
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "conduit:";
            });
            Console.WriteLine("[ConduitLLM.Admin] Distributed cache configured with Redis");
        }
        else
        {
            // Fallback to in-memory cache if Redis is not configured
            builder.Services.AddDistributedMemoryCache();
            Console.WriteLine("[ConduitLLM.Admin] WARNING: Using in-memory cache - ephemeral keys will not work across instances");
        }

        // Add SignalR with configuration
        var signalRBuilder = builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
            options.StreamBufferCapacity = 10;
        });

        // Add MessagePack protocol support with LZ4 compression
        // Enables both JSON (default) and MessagePack protocols for backward compatibility
        var messagePackEnabled = Environment.GetEnvironmentVariable("SIGNALR_MESSAGEPACK_ENABLED")?.ToLowerInvariant() != "false";
        if (messagePackEnabled)
        {
            signalRBuilder.AddMessagePackProtocol(options =>
            {
                // Configure MessagePack with security and compression
                options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                    .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                    .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData) // CVE-2020-5234 protection
                    .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray) // Use Lz4BlockArray for GC optimization
                    .WithCompressionMinLength(256); // Only compress messages > 256 bytes
            });
            Console.WriteLine("[ConduitLLM.Admin] SignalR configured with MessagePack protocol (LZ4 compression enabled)");
            Console.WriteLine("[ConduitLLM.Admin] SignalR supports both JSON and MessagePack protocols for backward compatibility");
        }
        else
        {
            Console.WriteLine("[ConduitLLM.Admin] SignalR configured with JSON protocol only (MessagePack disabled)");
        }

        // Configure SignalR Redis backplane for horizontal scaling if Redis is configured
        var signalRRedisConnectionString = builder.Configuration.GetConnectionString("RedisSignalR") ?? redisConnectionString;
        if (!string.IsNullOrEmpty(signalRRedisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(signalRRedisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = new StackExchange.Redis.RedisChannel("conduit_admin_signalr:", StackExchange.Redis.RedisChannel.PatternMode.Literal);
                options.Configuration.DefaultDatabase = 3; // Separate database for Admin SignalR
            });
            Console.WriteLine("[ConduitLLM.Admin] SignalR configured with Redis backplane for horizontal scaling");
        }
        else
        {
            Console.WriteLine("[ConduitLLM.Admin] SignalR configured without Redis backplane (single-instance mode)");
        }

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
                
                Console.WriteLine($"[ConduitLLM.Admin] Event bus configured with RabbitMQ transport (multi-instance mode) - Host: {rabbitMqConfig.Host}:{rabbitMqConfig.Port}");
                Console.WriteLine("[ConduitLLM.Admin] Event publishing ENABLED - Admin services will publish:");
                Console.WriteLine("  - VirtualKeyUpdated events (triggers cache invalidation in Gateway API)");
                Console.WriteLine("  - VirtualKeyDeleted events (triggers cache cleanup in Gateway API)");
                Console.WriteLine("  - ProviderUpdated events (triggers capability refresh)");
                Console.WriteLine("  - ProviderDeleted events (triggers cache cleanup)");
                Console.WriteLine("  - GlobalSettingChanged events (triggers cache invalidation in all instances)");
                Console.WriteLine("[ConduitLLM.Admin] Event consuming ENABLED - Admin services will consume:");
                Console.WriteLine("  - GlobalSettingChanged events (keeps Admin API cache synchronized)");
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
                
                Console.WriteLine("[ConduitLLM.Admin] Event bus configured with in-memory transport (single-instance mode)");
                Console.WriteLine("[ConduitLLM.Admin] Event publishing and consuming ENABLED - Events will be processed locally");
                Console.WriteLine("[ConduitLLM.Admin] WARNING: For production multi-instance deployments, configure RabbitMQ");
                Console.WriteLine("  - This ensures Gateway API instances receive cache invalidation events");
                Console.WriteLine("  - Without RabbitMQ, only the local Gateway API instance will be notified");
                Console.WriteLine("[ConduitLLM.Admin] Event consuming ENABLED - Admin services will consume:");
                Console.WriteLine("  - GlobalSettingChanged events (keeps Admin API cache synchronized)");
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
            Console.WriteLine($"[ConduitLLM.Admin] OpenTelemetry tracing enabled - exporting to {otlpEndpoint}");
        }
        else
        {
            Console.WriteLine("[ConduitLLM.Admin] OpenTelemetry tracing disabled (set Telemetry:TracingEnabled=true to enable)");
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

        // Seed default data (e.g., default retention policy)
        await app.SeedDefaultDataAsync();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            // Map the OpenAPI endpoint
            app.MapOpenApi("/openapi/v1.json");

            // Map Scalar UI for interactive API documentation
            app.MapScalarApiReference();

            Console.WriteLine("[ConduitLLM.Admin] Scalar UI available at /scalar/v1");
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

        app.Run();
    }
}

// Make Program accessible for testing
public partial class Program { }
