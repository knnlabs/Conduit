// Facade for Gateway API security headers middleware
// Delegates to the shared ConduitLLM.Security.Middleware.SecurityHeadersMiddleware implementation
using Microsoft.AspNetCore.Builder;
using ConduitLLM.Security.Options;
using ConduitLLM.Security.Middleware;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Extension methods for adding Gateway security headers middleware.
    /// The actual implementation is in the shared ConduitLLM.Security library.
    /// </summary>
    public static class SecurityHeadersMiddlewareExtensions
    {
        /// <summary>
        /// Adds security headers middleware to the Gateway API application pipeline.
        /// Delegates to the shared SecurityHeadersMiddleware implementation.
        /// </summary>
        public static IApplicationBuilder UseCoreApiSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware<GatewaySecurityOptions>>();
        }
    }
}
