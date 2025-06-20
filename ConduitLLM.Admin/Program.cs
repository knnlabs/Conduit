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

        // Register MassTransit event bus with in-memory transport for Admin API
        // Note: Redis is not supported as a transport in MassTransit, only for saga persistence
        builder.Services.AddMassTransit(x =>
        {
            // Admin API primarily publishes events, minimal consumers
            // We'll add consumers here as needed
            
            x.UsingInMemory((context, cfg) =>
            {
                // NOTE: Using in-memory transport for single-instance deployments
                // Redis is used for caching and data protection, not message transport
                // For multi-instance production, consider RabbitMQ, Azure Service Bus, or Amazon SQS
                
                // Configure retry policy for reliability
                cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
                
                // Configure delayed redelivery for failed messages
                cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));
                
                // Configure endpoints
                cfg.ConfigureEndpoints(context);
            });
        });
        
        Console.WriteLine("[ConduitLLM.Admin] Event bus configured with in-memory transport (single-instance mode)");

        // Add standardized health checks
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddConduitHealthChecks(connectionString, redisConnectionString);

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
