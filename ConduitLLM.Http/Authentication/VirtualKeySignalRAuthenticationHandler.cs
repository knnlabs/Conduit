using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Custom authentication handler for SignalR hubs that validates virtual keys
    /// </summary>
    public class VirtualKeySignalRAuthenticationHandler : IAuthorizationHandler
    {
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<VirtualKeySignalRAuthenticationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of VirtualKeySignalRAuthenticationHandler
        /// </summary>
        public VirtualKeySignalRAuthenticationHandler(
            IVirtualKeyService virtualKeyService,
            ILogger<VirtualKeySignalRAuthenticationHandler> logger)
        {
            _virtualKeyService = virtualKeyService;
            _logger = logger;
        }

        /// <summary>
        /// Handles authorization for SignalR hub connections and invocations
        /// </summary>
        public async Task HandleAsync(AuthorizationHandlerContext context)
        {
            if (context.Resource is HubInvocationContext hubContext)
            {
                // This is a hub method invocation
                var connectionContext = hubContext.Context;
                
                // Check if already authenticated during connection
                if (connectionContext.Items.ContainsKey("VirtualKeyId"))
                {
                    // Connection was already authenticated
                    context.Succeed(context.PendingRequirements.First());
                    return;
                }
            }
            else if (context.Resource is HttpContext httpContext)
            {
                // This is the initial connection attempt
                var virtualKey = ExtractVirtualKey(httpContext);
                
                if (string.IsNullOrEmpty(virtualKey))
                {
                    _logger.LogWarning("Missing Virtual Key in SignalR connection from IP {IP}",
                        GetClientIpAddress(httpContext));
                    context.Fail();
                    return;
                }

                // Validate the Virtual Key
                var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
                if (keyEntity == null || !keyEntity.IsEnabled)
                {
                    _logger.LogWarning("Invalid or disabled Virtual Key in SignalR connection from IP {IP}",
                        GetClientIpAddress(httpContext));
                    context.Fail();
                    return;
                }

                // Create claims for the authenticated connection
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, keyEntity.KeyName ?? "Unknown"),
                    new Claim("VirtualKeyId", keyEntity.Id.ToString()),
                    new Claim("VirtualKey", virtualKey),
                    new Claim("VirtualKeyHash", keyEntity.KeyHash)
                };

                var identity = new ClaimsIdentity(claims, "VirtualKeySignalR");
                var principal = new ClaimsPrincipal(identity);
                
                // Replace the user context
                httpContext.User = principal;
                
                _logger.LogDebug("Successfully authenticated Virtual Key {KeyName} for SignalR connection",
                    keyEntity.KeyName);
                
                context.Succeed(context.PendingRequirements.First());
            }
            else
            {
                // Unknown resource type
                context.Fail();
            }
        }

        /// <summary>
        /// Extracts the Virtual Key from the request
        /// </summary>
        private string? ExtractVirtualKey(HttpContext context)
        {
            // Check query string first (for SignalR JavaScript clients)
            if (context.Request.Query.TryGetValue("access_token", out var queryToken))
            {
                return queryToken.ToString();
            }
            
            // Also check for api_key in query string
            if (context.Request.Query.TryGetValue("api_key", out var queryKey))
            {
                return queryKey.ToString();
            }

            // Try Authorization header (for .NET clients)
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // Try X-API-Key header
            var apiKeyHeader = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.Trim();
            }

            return null;
        }

        /// <summary>
        /// Gets the client IP address from the request
        /// </summary>
        private string GetClientIpAddress(HttpContext context)
        {
            // Check X-Forwarded-For header first (for reverse proxies)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ip = forwardedFor.Split(',').First().Trim();
                if (System.Net.IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }

            // Check X-Real-IP header
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && System.Net.IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }

    /// <summary>
    /// Authorization requirement for virtual key authentication
    /// </summary>
    public class VirtualKeySignalRAuthorizationRequirement : IAuthorizationRequirement
    {
    }
}