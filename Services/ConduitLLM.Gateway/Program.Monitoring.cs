using ConduitLLM.Configuration.Data;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Extensions;
using ConduitLLM.Gateway.Filters;

public partial class Program
{
    public static void ConfigureMonitoringServices(WebApplicationBuilder builder)
    {
        // Add Controller support
        builder.Services.AddControllers();

        // Operation-logging action filter — replaces the per-action success logging that used to
        // live in GatewayControllerBase.ExecuteAsync. Applied per controller via [ServiceFilter]
        // during the incremental Tier 1a migration (#902); promote to a global filter once all
        // Gateway controllers are converted.
        builder.Services.AddScoped<OperationLoggingFilter>();

        // Add OpenAPI support with Scalar
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ConduitLLM.Gateway.OpenApi.CoreApiDocumentTransformer>();
            options.AddOperationTransformer<ConduitLLM.Gateway.OpenApi.VirtualKeySecurityOperationTransformer>();
        });

        // Get Redis and RabbitMQ configuration for health checks
        var redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ResolveConnectionString();

        var connectionStringManager = new ConduitLLM.Core.Data.ConnectionStringManager();
        var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("CoreAPI");

        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Add standardized health checks (skip in test environment to avoid conflicts)
        if (builder.Environment.EnvironmentName != "Test")
        {
            // Add basic health checks
            var healthChecksBuilder = builder.Services.AddHealthChecks();

            // Add comprehensive RabbitMQ health check if RabbitMQ is configured AND the
            // MassTransit backend is active — the check injects MassTransit's IBus, which
            // is not registered on the Wolverine backend (#925; Wolverine-native health
            // checks land in #931).
            if (useRabbitMq
                && ConduitLLM.Configuration.Messaging.MessagingBackendResolver.Resolve(builder.Configuration)
                    == ConduitLLM.Configuration.Messaging.MessagingBackend.MassTransit)
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
                healthChecksBuilder.AddCheck<ConduitLLM.Gateway.HealthChecks.LeaderElectionHealthCheck>(
                    "leader_election",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                    tags: new[] { "leader_election", "background_services", "distributed" });
            }

        }

        // Add health monitoring services
        builder.Services.AddHealthMonitoring(builder.Configuration);

        // Add database migration services
        builder.Services.AddDatabaseMigration();

        // Add connection pool warmer with coordinated warming to prevent thundering herd during deployments
        // Unlike leader election, ALL instances warm their pools, but in a staggered manner
        builder.Services.AddCoordinatedConnectionPoolWarming(builder.Configuration, "CoreAPI");

        // Add cache statistics registration service
        builder.Services.AddHostedService<ConduitLLM.Gateway.Services.CacheStatisticsRegistrationService>();

        // Add business metrics service for Prometheus/Grafana dashboards
        // Uses leader election to avoid duplicate metrics collection in scaled-out deployments
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Gateway.Services.BusinessMetricsService>(
            serviceProvider =>
            {
                var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Gateway.Services.BusinessMetricsService>>();
                return new ConduitLLM.Gateway.Services.BusinessMetricsService(scopeFactory, logger);
            },
            "BusinessMetricsService");

        // Add gateway operations metrics service for operation-level metrics
        // Tracks LLM operations, batch operations, media operations, function executions, and routing decisions
        builder.Services.AddLeaderElectedHostedService<ConduitLLM.Gateway.Services.GatewayOperationsMetricsService>(
            serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Gateway.Services.GatewayOperationsMetricsService>>();
                return new ConduitLLM.Gateway.Services.GatewayOperationsMetricsService(serviceProvider, logger);
            },
            "GatewayOperationsMetricsService");
    }
}