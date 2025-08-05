using System.Reflection;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Caching;
using ConduitLLM.Providers.Extensions;

using MassTransit; // Added for event bus infrastructure

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Prometheus;

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
            });
        builder.Services.AddEndpointsApiExplorer();

        // Add HttpClient factory for provider connection testing
        builder.Services.AddHttpClient();
        
        // Add HttpClient for RabbitMQ Management API
        builder.Services.AddHttpClient<IRabbitMQManagementClient, RabbitMQManagementClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Configure Swagger with XML comments
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ConduitLLM Admin API",
                Version = "v1",
                Description = "Administrative API for ConduitLLM",
                Contact = new OpenApiContact
                {
                    Name = "ConduitLLM Team"
                }
            });

            // Use fully qualified type names to avoid schema ID conflicts
            c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

            // Add XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);

            // Add security definition for API Key
            c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "API Key Authentication"
            });

            // Add security requirement for API Key
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        }
                    },
                    new string[] { }
                }
            });
        });

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

        // Add SignalR with configuration
        var signalRBuilder = builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
            options.StreamBufferCapacity = 10;
        });

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

        // Register MassTransit event bus for Admin API
        builder.Services.AddMassTransit(x =>
        {
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
                Console.WriteLine("  - VirtualKeyUpdated events (triggers cache invalidation in Core API)");
                Console.WriteLine("  - VirtualKeyDeleted events (triggers cache cleanup in Core API)");
                Console.WriteLine("  - ProviderUpdated events (triggers capability refresh)");
                Console.WriteLine("  - ProviderDeleted events (triggers cache cleanup)");
                Console.WriteLine("[ConduitLLM.Admin] Event consuming ENABLED - Admin services will consume:");
                Console.WriteLine("  - ProviderHealthChanged events (forwards to Admin SignalR clients)");
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
                Console.WriteLine("  - This ensures Core API instances receive cache invalidation events");
                Console.WriteLine("  - Without RabbitMQ, only the local Core API instance will be notified");
                Console.WriteLine("[ConduitLLM.Admin] Event consuming ENABLED - Admin services will consume:");
                Console.WriteLine("  - ProviderHealthChanged events (forwards to Admin SignalR clients)");
            }
        });

        // Add basic health checks
        builder.Services.AddHealthChecks();
        
        // Add connection pool warmer for better startup performance
        builder.Services.AddHostedService<ConduitLLM.Core.Services.ConnectionPoolWarmer>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.ConnectionPoolWarmer>>();
            return new ConduitLLM.Core.Services.ConnectionPoolWarmer(serviceProvider, logger, "AdminAPI");
        });

        // Configure OpenTelemetry metrics
        builder.Services.AddOpenTelemetry()
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

        // Add monitoring services
        builder.Services.AddHostedService<ConduitLLM.Admin.Services.AdminOperationsMetricsService>();
        
        // Add error queue metrics collection service
        builder.Services.AddHostedService<ConduitLLM.Admin.Services.ErrorQueueMetricsService>();
        
        // Add cache infrastructure before monitoring services
        builder.Services.AddCacheInfrastructure(builder.Configuration);
        
        // Add cache monitoring and alerting services
        // Temporarily disabled due to health check issues
        // builder.Services.AddCacheMonitoring(builder.Configuration);
        // builder.Services.AddHostedService<ConduitLLM.Admin.Services.CacheAlertNotificationService>();

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

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Only use HTTPS redirection if explicitly enabled
        var enableHttpsRedirection = Environment.GetEnvironmentVariable("CONDUIT_ENABLE_HTTPS_REDIRECTION") != "false";
        if (enableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

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
            Predicate = check => check.Tags.Contains("ready") || !check.Tags.Any()
        });

        // Map Prometheus metrics endpoint - requires authentication
        app.UseOpenTelemetryPrometheusScrapingEndpoint(
            context => context.Request.Path == "/metrics" && 
                      (context.User.Identity?.IsAuthenticated ?? false)
        );

        // Alternative: Map metrics endpoint without authentication (for monitoring systems)
        // app.UseOpenTelemetryPrometheusScrapingEndpoint();

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
