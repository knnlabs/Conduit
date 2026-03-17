using ConduitLLM.Configuration.Data;
using ConduitLLM.Core.Middleware;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Security.Middleware;
using Scalar.AspNetCore;

public partial class Program
{
    public static async Task ConfigureMiddleware(WebApplication app)
    {
        // Log deprecation warnings and validate Redis URL
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            ConduitLLM.Configuration.Extensions.DeprecationWarnings.LogEnvironmentVariableDeprecations(logger);

            // Validate Redis URL if provided
            var envRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(envRedisUrl))
            {
                ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "Http Service");
            }
        }

        // Run database migrations
        await app.RunDatabaseMigrationAsync();

        Console.WriteLine("[Conduit] Database initialization phase completed, configuring middleware...");

        // Add correlation ID middleware (earliest — establishes correlation context for all downstream middleware)
        app.UseCorrelationId();

        // Add request tracking middleware (after correlation ID, wraps entire request lifecycle)
        app.UseGatewayRequestTracking();

        // Enable CORS
        app.UseCors();
        Console.WriteLine("[Conduit] CORS configured");

        // Add health endpoint authorization (early in pipeline, before authentication)
        // This protects health endpoints from external access without valid key
        app.UseHealthEndpointAuthorization();
        Console.WriteLine("[Conduit] Health endpoint authorization configured");

        // Enable Scalar API documentation in development
        if (app.Environment.IsDevelopment())
        {
            // Map the OpenAPI endpoint
            app.MapOpenApi("/openapi/v1.json");

            // Map Scalar UI for interactive API documentation
            app.MapScalarApiReference();

            Console.WriteLine("[Conduit] Scalar UI available at /scalar/v1");
        }

        // Add security headers
        app.UseGatewaySecurityHeaders();

        // Add Redis availability check middleware (must be early in pipeline)
        app.UseRedisAvailability();
        Console.WriteLine("[Conduit] Redis circuit breaker middleware configured");

        // Add authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Add ephemeral key cleanup middleware (must be after authentication)
        app.UseMiddleware<EphemeralKeyCleanupMiddleware>();

        // Note: VirtualKeyAuthenticationHandler is now used instead of middleware
        // The authentication handler is registered with the "VirtualKey" scheme above

        // Add OpenAI error handling middleware to map exceptions to proper HTTP status codes
        app.UseOpenAIErrorHandling();
        Console.WriteLine("[Conduit] OpenAI error handling middleware configured");

        // Add usage tracking middleware to capture LLM usage from responses
        app.UseUsageTracking();
        Console.WriteLine("[Conduit] Usage tracking middleware configured");

        // Add HTTP metrics middleware for comprehensive request tracking
        app.UseMiddleware<ConduitLLM.Gateway.Middleware.HttpMetricsMiddleware>();

        // Add security middleware (IP filtering, rate limiting, ban checks)
        app.UseCoreApiSecurity();

        // Enable rate limiting (now that Virtual Keys are authenticated)
        app.UseRateLimiter();

        // Add timeout diagnostics middleware
        app.UseMiddleware<ConduitLLM.Core.Middleware.TimeoutDiagnosticsMiddleware>();

        // Enable WebSockets for real-time communication
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(120)
        });

        // Add controllers to the app
        app.MapControllers();
        Console.WriteLine("[Gateway API] Controllers registered");
    }
}