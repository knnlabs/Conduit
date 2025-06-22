using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Configuration.HealthChecks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for configuring health checks across all Conduit services.
    /// </summary>
    public static class HealthCheckExtensions
    {
        /// <summary>
        /// Adds standardized health checks for Conduit services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="redisConnection">Redis connection string (optional).</param>
        /// <param name="includeProviderCheck">Whether to include provider health check (default: true).</param>
        /// <param name="rabbitMqConfig">RabbitMQ configuration (optional).</param>
        /// <returns>The configured health check builder.</returns>
        public static IHealthChecksBuilder AddConduitHealthChecks(
            this IServiceCollection services,
            string? connectionString = null,
            string? redisConnection = null,
            bool includeProviderCheck = true,
            RabbitMqConfiguration? rabbitMqConfig = null)
        {
            var healthChecksBuilder = services.AddHealthChecks();

            // Add database health check if connection string is provided
            if (!string.IsNullOrEmpty(connectionString))
            {
                healthChecksBuilder.AddTypeActivatedCheck<DatabaseHealthCheck>(
                    "database",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "db", "sql", "ready" },
                    args: new object[] { connectionString });
            }

            // Add Redis health check if connection string is provided
            if (!string.IsNullOrEmpty(redisConnection))
            {
                healthChecksBuilder.AddTypeActivatedCheck<RedisHealthCheck>(
                    "redis",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "cache", "redis", "ready" },
                    args: new object[] { redisConnection });
            }

            // Add RabbitMQ health check if configuration is provided
            if (rabbitMqConfig != null && !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost")
            {
                healthChecksBuilder.AddTypeActivatedCheck<RabbitMqHealthCheck>(
                    "rabbitmq",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "messaging", "rabbitmq", "ready" },
                    args: new object[] { rabbitMqConfig });
            }

            // Add provider health check only if requested and repositories are available
            if (includeProviderCheck)
            {
                healthChecksBuilder.AddCheck<ProviderHealthCheck>(
                    "providers",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "providers", "llm", "ready" });
            }

            return healthChecksBuilder;
        }

        /// <summary>
        /// Maps standardized health check endpoints.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseConduitHealthChecks(this IApplicationBuilder app)
        {
            app.UseHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false, // No checks, just return 200 if the app is running
                ResponseWriter = WriteHealthCheckResponse
            });

            app.UseHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = WriteHealthCheckResponse
            });

            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse
            });

            return app;
        }

        /// <summary>
        /// Maps standardized health check endpoints for minimal APIs.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <returns>The web application.</returns>
        public static WebApplication MapConduitHealthChecks(this WebApplication app)
        {
            // Check if health checks are registered
            var healthCheckService = app.Services.GetService<HealthCheckService>();
            if (healthCheckService == null)
            {
                // Health checks not registered (e.g., in test environment)
                // Map basic endpoints that return success with proper format
                var basicHealthResponse = new
                {
                    status = "Healthy",
                    checks = Array.Empty<object>(),
                    totalDuration = 0.0
                };
                
                app.MapGet("/health/live", () => Results.Ok(basicHealthResponse));
                app.MapGet("/health/ready", () => Results.Ok(basicHealthResponse));
                app.MapGet("/health", () => Results.Ok(basicHealthResponse));
                return app;
            }

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false, // No checks, just return 200 if the app is running
                ResponseWriter = WriteHealthCheckResponse
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = WriteHealthCheckResponse
            });

            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse
            });

            return app;
        }

        private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            // Set appropriate status code based on health status
            context.Response.StatusCode = report.Status switch
            {
                HealthStatus.Healthy => 200,
                HealthStatus.Degraded => 200, // Return 200 for degraded so Docker considers it healthy
                HealthStatus.Unhealthy => 503,
                _ => 503 // Default to unhealthy for any unknown status
            };

            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    duration = x.Value.Duration.TotalMilliseconds,
                    exception = x.Value.Exception?.Message,
                    data = x.Value.Data
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }
    }
}
