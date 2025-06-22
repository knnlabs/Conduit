using System.Reflection;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Providers.Extensions;

using MassTransit; // Added for event bus infrastructure

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

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
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        // Add HttpClient factory for provider connection testing
        builder.Services.AddHttpClient();

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

        // Configure RabbitMQ settings
        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Register MassTransit event bus for Admin API
        builder.Services.AddMassTransit(x =>
        {
            // Admin API is a publisher-only service, no consumers needed
            
            if (useRabbitMq)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    // Configure RabbitMQ connection
                    cfg.Host(new Uri($"rabbitmq://{rabbitMqConfig.Host}:{rabbitMqConfig.Port}{rabbitMqConfig.VHost}"), h =>
                    {
                        h.Username(rabbitMqConfig.Username);
                        h.Password(rabbitMqConfig.Password);
                        h.Heartbeat(TimeSpan.FromSeconds(rabbitMqConfig.HeartbeatInterval));
                    });
                    
                    // Configure retry policy for publishing
                    cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));
                    
                    // Admin API only publishes events, no special configuration needed
                });
                
                Console.WriteLine($"[ConduitLLM.Admin] Event bus configured with RabbitMQ transport (multi-instance mode) - Host: {rabbitMqConfig.Host}:{rabbitMqConfig.Port}");
                Console.WriteLine("[ConduitLLM.Admin] Event publishing ENABLED - Admin services will publish:");
                Console.WriteLine("  - VirtualKeyUpdated events (triggers cache invalidation in Core API)");
                Console.WriteLine("  - VirtualKeyDeleted events (triggers cache cleanup in Core API)");
                Console.WriteLine("  - ProviderCredentialUpdated events (triggers capability refresh)");
                Console.WriteLine("  - ProviderCredentialDeleted events (triggers cache cleanup)");
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
                Console.WriteLine("[ConduitLLM.Admin] Event publishing ENABLED - Events will be processed locally");
                Console.WriteLine("[ConduitLLM.Admin] WARNING: For production multi-instance deployments, configure RabbitMQ");
                Console.WriteLine("  - This ensures Core API instances receive cache invalidation events");
                Console.WriteLine("  - Without RabbitMQ, only the local Core API instance will be notified");
            }
        });

        // Add standardized health checks
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddConduitHealthChecks(connectionString, redisConnectionString, false, rabbitMqConfig);

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

        // Initialize database - Always run unless explicitly told to skip
        // This ensures users get automatic schema updates when pulling new versions
        var skipDatabaseInit = Environment.GetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT") == "true";
        if (!skipDatabaseInit)
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.Data.DatabaseInitializer>();
                var initLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    initLogger.LogInformation("Starting database initialization for Admin API...");

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
                        throw new InvalidOperationException($"Database initialization failed after {maxRetries} attempts. Please check database connectivity and logs.");
                    }
                }
                catch (Exception ex)
                {
                    initLogger.LogError(ex, "Critical error during database initialization");
                    throw new InvalidOperationException("Failed to initialize database. Application cannot start.", ex);
                }
            }
        }

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

        app.UseAuthorization();

        app.MapControllers();

        // Map standardized health check endpoints
        app.MapConduitHealthChecks();

        app.Run();
    }
}

// Make Program accessible for testing
public partial class Program { }
