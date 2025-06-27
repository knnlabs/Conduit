using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware that handles Virtual Key authentication for Core API endpoints
    /// </summary>
    public class VirtualKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<VirtualKeyAuthenticationMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyAuthenticationMiddleware
        /// </summary>
        public VirtualKeyAuthenticationMiddleware(
            RequestDelegate next,
            ILogger<VirtualKeyAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Processes the HTTP request for Virtual Key authentication
        /// </summary>
        public async Task InvokeAsync(HttpContext context, IVirtualKeyService virtualKeyService)
        {
            // Skip authentication for excluded paths
            if (IsPathExcluded(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Extract Virtual Key from request
            var virtualKey = ExtractVirtualKey(context);
            
            if (string.IsNullOrEmpty(virtualKey))
            {
                _logger.LogWarning("Missing Virtual Key in request to {Path} from IP {IP}", 
                    context.Request.Path, 
                    context.Connection.RemoteIpAddress);
                
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid authentication" });
                return;
            }
            
            _logger.LogInformation("Extracted Virtual Key: {KeyPrefix}... (length: {Length})", 
                virtualKey.Length > 10 ? virtualKey.Substring(0, 10) : virtualKey,
                virtualKey.Length);

            // Validate Virtual Key
            var validatedKey = await virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
            
            if (validatedKey == null)
            {
                _logger.LogWarning("Invalid Virtual Key attempt for key {Key} from IP {IP}", 
                    virtualKey.Substring(0, Math.Min(10, virtualKey.Length)) + "...", 
                    context.Connection.RemoteIpAddress);
                
                // Store failed attempt info for security service to track
                context.Items["FailedAuth"] = true;
                context.Items["FailedAuthReason"] = "Invalid Virtual Key";
                context.Items["AttemptedKey"] = virtualKey;
                
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid Virtual Key" });
                return;
            }

            // Check if key is enabled
            if (!validatedKey.IsEnabled)
            {
                _logger.LogWarning("Disabled Virtual Key attempt for key {KeyName} from IP {IP}", 
                    validatedKey.KeyName, 
                    context.Connection.RemoteIpAddress);
                
                // Store failed attempt info for security service to track
                context.Items["FailedAuth"] = true;
                context.Items["FailedAuthReason"] = "Virtual Key is disabled";
                context.Items["AttemptedKey"] = virtualKey;
                
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Virtual Key is disabled" });
                return;
            }

            // Check if key is expired
            if (validatedKey.ExpiresAt.HasValue && validatedKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired Virtual Key attempt for key {KeyName} from IP {IP}", 
                    validatedKey.KeyName, 
                    context.Connection.RemoteIpAddress);
                
                // Store failed attempt info for security service to track
                context.Items["FailedAuth"] = true;
                context.Items["FailedAuthReason"] = "Virtual Key has expired";
                context.Items["AttemptedKey"] = virtualKey;
                
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Virtual Key has expired" });
                return;
            }

            // Set authentication context
            var claims = new[]
            {
                new Claim("VirtualKeyId", validatedKey.Id.ToString()),
                new Claim("VirtualKeyName", validatedKey.KeyName ?? ""),
                new Claim(ClaimTypes.AuthenticationMethod, "VirtualKey")
            };

            var identity = new ClaimsIdentity(claims, "VirtualKey");
            var principal = new ClaimsPrincipal(identity);
            context.User = principal;

            // Store Virtual Key info in context for downstream use
            context.Items["VirtualKey"] = virtualKey;
            context.Items["VirtualKeyId"] = validatedKey.Id;
            context.Items["VirtualKeyName"] = validatedKey.KeyName;
            context.Items["VirtualKeyEntity"] = validatedKey;

            // Clear any previous failed auth tracking for this IP (will be handled by SecurityService)
            context.Items["AuthSuccess"] = true;

            _logger.LogDebug("Virtual Key {KeyName} authenticated successfully for {Path}", 
                validatedKey.KeyName, 
                context.Request.Path);

            await _next(context);
        }

        private string? ExtractVirtualKey(HttpContext context)
        {
            // Check Authorization header (Bearer token)
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            // Check api-key header (OpenAI compatible)
            var apiKeyHeader = context.Request.Headers["api-key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader;
            }

            // Check X-API-Key header (alternative)
            var xApiKeyHeader = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xApiKeyHeader))
            {
                return xApiKeyHeader;
            }

            // Check X-Virtual-Key header (legacy support)
            var xVirtualKeyHeader = context.Request.Headers["X-Virtual-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xVirtualKeyHeader))
            {
                return xVirtualKeyHeader;
            }

            return null;
        }

        private bool IsPathExcluded(PathString path)
        {
            // Exclude health checks, metrics, documentation, public media, and SignalR hubs
            var excludedPaths = new[]
            {
                "/health",
                "/health/live",
                "/health/ready",
                "/metrics",
                "/swagger",
                "/_framework",
                "/favicon.ico",
                "/v1/media/public",
                "/hubs"  // SignalR hubs use different authentication mechanism
            };

            return excludedPaths.Any(excluded => 
                path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Extension methods for VirtualKeyAuthenticationMiddleware
    /// </summary>
    public static class VirtualKeyAuthenticationMiddlewareExtensions
    {
        /// <summary>
        /// Adds Virtual Key authentication middleware to the pipeline
        /// </summary>
        public static IApplicationBuilder UseVirtualKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<VirtualKeyAuthenticationMiddleware>();
        }
    }
}