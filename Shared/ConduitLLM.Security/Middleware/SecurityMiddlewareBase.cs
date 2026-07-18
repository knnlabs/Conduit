using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.Security.Models;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Security.Middleware
{
    /// <summary>
    /// Base class for security middleware that provides common security check flow.
    /// Derived classes can add API-specific security handling.
    /// </summary>
    public abstract class SecurityMiddlewareBase
    {
        /// <summary>
        /// The next middleware in the pipeline
        /// </summary>
        protected readonly RequestDelegate Next;

        /// <summary>
        /// Logger instance for security events
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the security middleware base
        /// </summary>
        protected SecurityMiddlewareBase(RequestDelegate next, ILogger logger)
        {
            Next = next ?? throw new ArgumentNullException(nameof(next));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes the HTTP request through security checks.
        /// Template method that calls the derived class's security check implementation.
        /// </summary>
        protected async Task ProcessRequestAsync(HttpContext context, Func<HttpContext, Task<SecurityCheckResult>> securityCheck)
        {
            var clientIp = GetClientIpAddress(context);

            // Check for early exit conditions (e.g., prior authentication failure)
            if (ShouldSkipSecurityCheck(context))
            {
                return;
            }

            // Perform the security check
            var result = await securityCheck(context);

            if (!result.IsAllowed)
            {
                await HandleSecurityViolationAsync(context, result, clientIp);
                return;
            }

            await Next(context);
        }

        /// <summary>
        /// Determines whether to skip security checks for this request.
        /// Override in derived classes to add API-specific skip conditions.
        /// </summary>
        protected virtual bool ShouldSkipSecurityCheck(HttpContext context)
        {
            // Gateway-specific: if authentication already failed, don't continue
            if (context.Response.StatusCode == 401)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Handles a security violation by logging, recording events, and sending the error response.
        /// Override OnSecurityViolationAsync for additional handling (e.g., event monitoring).
        /// </summary>
        protected virtual async Task HandleSecurityViolationAsync(HttpContext context, SecurityCheckResult result, string clientIp)
        {
            Logger.LogWarning("Request blocked: {Reason} for path {Path} from IP {IP}",
                result.Reason,
                context.Request.Path,
                clientIp);

            // Allow derived classes to record events or perform additional actions
            await OnSecurityViolationAsync(context, result, clientIp);

            context.Response.StatusCode = result.StatusCode ?? 403;

            // Add response headers (e.g., rate limit headers)
            foreach (var header in result.Headers)
            {
                context.Response.Headers.Append(header.Key, header.Value);
            }

            // Return JSON error response
            await context.Response.WriteAsJsonAsync(new
            {
                error = result.Reason,
                code = result.StatusCode
            });
        }

        /// <summary>
        /// Called when a security violation occurs, before the error response is sent.
        /// Override in derived classes to record security events.
        /// </summary>
        protected virtual Task OnSecurityViolationAsync(HttpContext context, SecurityCheckResult result, string clientIp)
        {
            // Default implementation does nothing - derived classes can override
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the client IP address from the request, considering proxy headers.
        /// </summary>
        protected virtual string GetClientIpAddress(HttpContext context)
        {
            return IpAddressHelper.GetClientIpAddress(context);
        }
    }
}
