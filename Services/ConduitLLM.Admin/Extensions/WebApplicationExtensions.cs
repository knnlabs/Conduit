using ConduitLLM.Admin.Middleware;
using ConduitLLM.Core.Middleware;
using ConduitLLM.Security.Middleware;

namespace ConduitLLM.Admin.Extensions;

/// <summary>
/// Extension methods for configuring Admin API middleware in the application pipeline
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Adds Admin API middleware to the HTTP request pipeline
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication UseAdminMiddleware(this WebApplication app)
    {
        // Enable request body buffering so it can be re-read for error diagnostics
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next();
        });

        // Add correlation ID middleware (earliest — establishes correlation context for all downstream middleware)
        app.UseCorrelationId();

        // Add CORS middleware
        app.UseCors("AdminCorsPolicy");

        // Add security headers middleware
        app.UseAdminSecurityHeaders();

        // Add unified security middleware (replaces AdminAuthenticationMiddleware)
        app.UseAdminSecurity();

        // Add Ephemeral Master Key cleanup middleware
        app.UseMiddleware<EphemeralMasterKeyCleanupMiddleware>();

        // Add global exception handling middleware (catches exceptions from downstream middleware and controllers)
        app.UseAdminExceptionHandling();

        // Add HTTP metrics middleware
        app.UseMiddleware<AdminHttpMetricsMiddleware>();

        // Add request tracking middleware
        app.UseMiddleware<AdminRequestTrackingMiddleware>();

        return app;
    }
}
