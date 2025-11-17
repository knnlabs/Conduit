using ConduitLLM.Configuration.Data;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Http.Extensions;

public partial class Program
{
    public static void ConfigureMonitoringServices(WebApplicationBuilder builder)
    {
        // Add Controller support
        builder.Services.AddControllers();

        // Add OpenAPI support with Scalar
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ConduitLLM.Http.OpenApi.CoreApiDocumentTransformer>();
            options.AddOperationTransformer<ConduitLLM.Http.OpenApi.VirtualKeySecurityOperationTransformer>();
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

            // Add Redis health check if Redis is configured
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                var redisConnStr = redisConnectionString; // Capture for closure
                healthChecksBuilder.AddTypeActivatedCheck<ConduitLLM.Configuration.HealthChecks.RedisHealthCheck>(
                    "redis",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new[] { "cache", "redis", "billing" },
                    args: new object[] { redisConnStr });
                
                // Add circuit breaker health check
                healthChecksBuilder.AddCheck<ConduitLLM.Configuration.HealthChecks.RedisCircuitBreakerHealthCheck>(
                    "redis_circuit_breaker",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new[] { "circuit_breaker", "redis", "resilience" });
                
                // Add leader election health check
                healthChecksBuilder.AddCheck<ConduitLLM.Http.HealthChecks.LeaderElectionHealthCheck>(
                    "leader_election",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                    tags: new[] { "leader_election", "background_services", "distributed" });
            }

            // Audio health checks removed per YAGNI principle
            
            // Add advanced health monitoring checks (includes SignalR and HTTP connection pool checks)
            healthChecksBuilder.AddAdvancedHealthMonitoring(builder.Configuration);
        }

        // Add health monitoring services
        builder.Services.AddHealthMonitoring(builder.Configuration);

        // Add database migration services
        builder.Services.AddDatabaseMigration();

        // Add connection pool warmer for better startup performance - with leader election
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Core.Services.ConnectionPoolWarmer>(
            serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.ConnectionPoolWarmer>>();
                return new ConduitLLM.Core.Services.ConnectionPoolWarmer(serviceProvider, logger, "CoreAPI");
            },
            "ConnectionPoolWarmer");

        // Add cache statistics registration service
        builder.Services.AddHostedService<ConduitLLM.Http.Services.CacheStatisticsRegistrationService>();
    }
}