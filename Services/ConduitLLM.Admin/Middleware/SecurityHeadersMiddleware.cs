// Facade for Admin API security headers middleware
// Delegates to the shared ConduitLLM.Security.Middleware.SecurityHeadersMiddleware implementation
using Microsoft.AspNetCore.Builder;
using ConduitLLM.Security.Options;
using ConduitLLM.Security.Middleware;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Extension methods for adding Admin security headers middleware.
    /// The actual implementation is in the shared ConduitLLM.Security library.
    /// </summary>
    public static class SecurityHeadersMiddlewareExtensions
    {
        /// <summary>
        /// Adds security headers middleware to the Admin API application pipeline.
        /// Delegates to the shared SecurityHeadersMiddleware implementation.
        /// </summary>
        public static IApplicationBuilder UseAdminSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware<AdminSecurityOptions>>();
        }
    }
}
