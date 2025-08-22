using ConduitLLM.Configuration.Data;
using ConduitLLM.Http.Extensions;

public partial class Program
{
    public static void ConfigureMonitoringServices(WebApplicationBuilder builder)
    {
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

        // Get Redis and RabbitMQ configuration for health checks
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

        var connectionStringManager = new ConduitLLM.Core.Data.ConnectionStringManager();
        var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("CoreAPI", msg => Console.WriteLine(msg));

        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Add standardized health checks (skip in test environment to avoid conflicts)
        if (builder.Environment.EnvironmentName != "Test")
        {
            // Add basic health checks
            var healthChecksBuilder = builder.Services.AddHealthChecks();

            // Add comprehensive RabbitMQ health check if RabbitMQ is configured
            if (useRabbitMq)
            {
                healthChecksBuilder.AddCheck<ConduitLLM.Core.HealthChecks.RabbitMQHealthCheck>(
                    "rabbitmq_comprehensive",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new[] { "messaging", "rabbitmq", "performance", "monitoring" });
            }

            // Audio health checks removed per YAGNI principle
            
            // Add advanced health monitoring checks (includes SignalR and HTTP connection pool checks)
            healthChecksBuilder.AddAdvancedHealthMonitoring(builder.Configuration);
        }

        // Add health monitoring services
        builder.Services.AddHealthMonitoring(builder.Configuration);

        // Add database migration services
        builder.Services.AddDatabaseMigration();

        // Add connection pool warmer for better startup performance
        builder.Services.AddHostedService<ConduitLLM.Core.Services.ConnectionPoolWarmer>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.ConnectionPoolWarmer>>();
            return new ConduitLLM.Core.Services.ConnectionPoolWarmer(serviceProvider, logger, "CoreAPI");
        });

        // Add cache statistics registration service
        builder.Services.AddHostedService<ConduitLLM.Http.Services.CacheStatisticsRegistrationService>();
    }
}