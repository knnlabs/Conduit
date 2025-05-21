using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Middleware
{
    /// <summary>
    /// Middleware for handling Admin API connection issues
    /// </summary>
    /// <remarks>
    /// This middleware checks the health of the Admin API connection and
    /// displays appropriate error messages when the connection fails.
    /// </remarks>
    public class AdminApiConnectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AdminApiConnectionMiddleware> _logger;

        /// <summary>
        /// Creates a new instance of the <see cref="AdminApiConnectionMiddleware"/>
        /// </summary>
        /// <param name="next">The next request delegate</param>
        /// <param name="logger">The logger</param>
        public AdminApiConnectionMiddleware(RequestDelegate next, ILogger<AdminApiConnectionMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <param name="healthService">The Admin API health service</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task InvokeAsync(HttpContext context, IAdminApiHealthService healthService)
        {
            // Check if we're accessing an API endpoint or static file - skip health check for these
            var path = context.Request.Path.ToString().ToLowerInvariant();
            if (path.StartsWith("/api/") || 
                path.Contains(".") || // Static files usually have extensions
                path.StartsWith("/_") || // Blazor resources
                path == "/favicon.ico")
            {
                await _next(context);
                return;
            }

            // Check the Admin API health
            var isHealthy = await healthService.CheckHealthAsync();
            if (!isHealthy && !context.Request.Path.StartsWithSegments("/error"))
            {
                // Get the health service connection details
                var connectionDetails = healthService.GetConnectionDetails();
                var hasAuthError = connectionDetails.LastErrorMessage.Contains("authentication") || 
                                 connectionDetails.LastErrorMessage.Contains("Unauthorized") ||
                                 connectionDetails.LastErrorMessage.Contains("Forbidden");
                
                // Store the original path so we can redirect back after the connection is restored
                context.Items["OriginalPath"] = context.Request.Path;
                
                // Redirect to appropriate error page based on the error type
                if (hasAuthError)
                {
                    _logger.LogWarning("Admin API authentication failed. Displaying auth error page.");
                    context.Response.Redirect("/error/admin-api-auth");
                }
                else
                {
                    _logger.LogWarning("Admin API connection is not healthy. Displaying connection error page.");
                    context.Response.Redirect("/error/admin-api-connection");
                }
                return;
            }

            await _next(context);
        }
    }
}