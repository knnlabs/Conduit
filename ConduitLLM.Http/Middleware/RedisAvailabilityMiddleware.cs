using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Http.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware that blocks requests when Redis circuit breaker is open
    /// </summary>
    public class RedisAvailabilityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RedisAvailabilityMiddleware> _logger;
        private readonly RedisCircuitBreakerOptions _options;

        public RedisAvailabilityMiddleware(
            RequestDelegate next,
            ILogger<RedisAvailabilityMiddleware> logger,
            IOptions<RedisCircuitBreakerOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task InvokeAsync(HttpContext context, IRedisCircuitBreaker circuitBreaker)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Check if this path should bypass the circuit breaker
            if (ShouldBypass(path))
            {
                await _next(context);
                return;
            }

            // Check circuit breaker state
            if (circuitBreaker.IsOpen)
            {
                await HandleCircuitOpen(context, circuitBreaker);
                return;
            }

            // Allow request to proceed
            await _next(context);
        }

        private bool ShouldBypass(string path)
        {
            // Always bypass certain critical paths
            var criticalPaths = new[] { "/health", "/metrics", "/swagger" };
            if (criticalPaths.Any(cp => path.StartsWith(cp, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check configured bypass paths
            if (_options.BypassPaths != null)
            {
                return _options.BypassPaths.Any(bp => 
                    path.StartsWith(bp.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        private async Task HandleCircuitOpen(HttpContext context, IRedisCircuitBreaker circuitBreaker)
        {
            var stats = circuitBreaker.Statistics;
            
            _logger.LogWarning(
                "Request rejected due to open Redis circuit breaker. Path: {Path}, IP: {IP}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            // Set response status code
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.ContentType = "application/json";

            // Add Retry-After header if we know when circuit might close
            if (stats.TimeUntilHalfOpen != null && stats.TimeUntilHalfOpen.Value.TotalSeconds > 0)
            {
                context.Response.Headers.Append("Retry-After", 
                    Math.Ceiling(stats.TimeUntilHalfOpen.Value.TotalSeconds).ToString());
            }

            // Add custom headers for monitoring
            context.Response.Headers.Append("X-Circuit-Breaker-State", "Open");
            context.Response.Headers.Append("X-Service-Status", "Degraded");

            // Prepare error response
            var response = new
            {
                error = new
                {
                    code = "SERVICE_UNAVAILABLE",
                    message = _options.OpenCircuitMessage,
                    details = _options.IncludeErrorDetails ? new
                    {
                        circuit_state = stats.State.ToString(),
                        total_failures = stats.TotalFailures,
                        rejected_requests = stats.RejectedRequests,
                        last_failure_at = stats.LastFailureAt?.ToString("O"),
                        circuit_opened_at = stats.CircuitOpenedAt?.ToString("O"),
                        retry_after_seconds = stats.TimeUntilHalfOpen?.TotalSeconds
                    } : null
                },
                timestamp = DateTime.UtcNow.ToString("O"),
                path = context.Request.Path.Value,
                request_id = context.TraceIdentifier
            };

            // Write response
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var jsonResponse = JsonSerializer.Serialize(response, jsonOptions);
            await context.Response.WriteAsync(jsonResponse);

            // Record metrics (will be implemented in metrics step)
            RecordRejectedRequest(context);
        }

        private void RecordRejectedRequest(HttpContext context)
        {
            // Record Prometheus metrics
            BillingMetrics.RecordCircuitBreakerRejection(
                context.Request.Path.Value ?? "unknown",
                context.Request.Method);
            
            _logger.LogInformation(
                "Redis circuit breaker rejected request. Method: {Method}, Path: {Path}, User: {User}",
                context.Request.Method,
                context.Request.Path,
                context.User?.Identity?.Name ?? "Anonymous");
        }
    }

    /// <summary>
    /// Extension methods for registering RedisAvailabilityMiddleware
    /// </summary>
    public static class RedisAvailabilityMiddlewareExtensions
    {
        /// <summary>
        /// Adds Redis availability checking middleware to the pipeline
        /// </summary>
        public static IApplicationBuilder UseRedisAvailability(this IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            return app.UseMiddleware<RedisAvailabilityMiddleware>();
        }
    }
}