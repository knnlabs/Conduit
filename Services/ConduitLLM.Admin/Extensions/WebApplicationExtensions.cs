using ConduitLLM.Admin.Middleware;

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
        // Add CORS middleware
        app.UseCors("AdminCorsPolicy");

        // Add security headers middleware
        app.UseAdminSecurityHeaders();

        // Add unified security middleware (replaces AdminAuthenticationMiddleware)
        app.UseAdminSecurity();

        // Add Ephemeral Master Key cleanup middleware
        app.UseMiddleware<EphemeralMasterKeyCleanupMiddleware>();

        // Add HTTP metrics middleware
        app.UseMiddleware<AdminHttpMetricsMiddleware>();

        // Add request tracking middleware
        app.UseMiddleware<AdminRequestTrackingMiddleware>();

        return app;
    }
}
