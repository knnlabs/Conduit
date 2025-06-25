using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Hub filter that validates virtual keys for SignalR connections
    /// </summary>
    public class VirtualKeyHubFilter : IHubFilter
    {
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<VirtualKeyHubFilter> _logger;

        /// <summary>
        /// Initializes a new instance of VirtualKeyHubFilter
        /// </summary>
        public VirtualKeyHubFilter(
            IVirtualKeyService virtualKeyService,
            ILogger<VirtualKeyHubFilter> logger)
        {
            _virtualKeyService = virtualKeyService;
            _logger = logger;
        }

        /// <summary>
        /// Called when a new connection is established
        /// </summary>
        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            var httpContext = invocationContext.Context.GetHttpContext();
            
            // Check if already authenticated
            if (invocationContext.Context.Items.ContainsKey("VirtualKeyId"))
            {
                return await next(invocationContext);
            }

            // Extract and validate virtual key
            var virtualKey = ExtractVirtualKey(httpContext);
            
            if (string.IsNullOrEmpty(virtualKey))
            {
                _logger.LogWarning("Missing Virtual Key in SignalR method invocation: {Method}",
                    invocationContext.HubMethodName);
                throw new HubException("Authentication required");
            }

            // Validate the Virtual Key
            var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
            if (keyEntity == null || !keyEntity.IsEnabled)
            {
                _logger.LogWarning("Invalid or disabled Virtual Key in SignalR method invocation: {Method}",
                    invocationContext.HubMethodName);
                throw new HubException("Invalid authentication");
            }

            // Store virtual key information in the connection context
            invocationContext.Context.Items["VirtualKeyId"] = keyEntity.Id;
            invocationContext.Context.Items["VirtualKeyHash"] = keyEntity.KeyHash;
            invocationContext.Context.Items["VirtualKeyName"] = keyEntity.KeyName ?? "Unknown";
            invocationContext.Context.Items["VirtualKey"] = virtualKey;

            _logger.LogDebug("Authenticated Virtual Key {KeyName} for method {Method}",
                keyEntity.KeyName, invocationContext.HubMethodName);

            return await next(invocationContext);
        }

        /// <summary>
        /// Called when a client connects
        /// </summary>
        public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            var httpContext = context.Context.GetHttpContext();
            var virtualKey = ExtractVirtualKey(httpContext);
            
            if (string.IsNullOrEmpty(virtualKey))
            {
                _logger.LogWarning("Missing Virtual Key in SignalR connection from IP {IP}",
                    GetClientIpAddress(httpContext));
                context.Context.Abort();
                return;
            }

            // Validate the Virtual Key
            var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
            if (keyEntity == null || !keyEntity.IsEnabled)
            {
                _logger.LogWarning("Invalid or disabled Virtual Key in SignalR connection from IP {IP}",
                    GetClientIpAddress(httpContext));
                context.Context.Abort();
                return;
            }

            // Store virtual key information in the connection context
            context.Context.Items["VirtualKeyId"] = keyEntity.Id;
            context.Context.Items["VirtualKeyHash"] = keyEntity.KeyHash;
            context.Context.Items["VirtualKeyName"] = keyEntity.KeyName ?? "Unknown";
            context.Context.Items["VirtualKey"] = virtualKey;

            // Store claims in context items instead of modifying User
            // (User is read-only in SignalR hub context)

            _logger.LogInformation("Virtual Key {KeyName} connected to SignalR hub {Hub}",
                keyEntity.KeyName, context.Hub.GetType().Name);

            await next(context);
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public async Task OnDisconnectedAsync(
            HubLifetimeContext context,
            Exception? exception,
            Func<HubLifetimeContext, Exception?, Task> next)
        {
            var keyName = context.Context.Items.TryGetValue("VirtualKeyName", out var name) 
                ? name?.ToString() 
                : "Unknown";

            _logger.LogInformation("Virtual Key {KeyName} disconnected from SignalR hub {Hub}",
                keyName, context.Hub.GetType().Name);

            await next(context, exception);
        }

        /// <summary>
        /// Extracts the Virtual Key from the request
        /// </summary>
        private string? ExtractVirtualKey(Microsoft.AspNetCore.Http.HttpContext? httpContext)
        {
            if (httpContext == null) return null;

            // Check query string first (for SignalR JavaScript clients)
            if (httpContext.Request.Query.TryGetValue("access_token", out var queryToken))
            {
                return queryToken.ToString();
            }
            
            // Also check for api_key in query string
            if (httpContext.Request.Query.TryGetValue("api_key", out var queryKey))
            {
                return queryKey.ToString();
            }

            // Try Authorization header (for .NET clients)
            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // Try X-API-Key header
            var apiKeyHeader = httpContext.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.Trim();
            }

            return null;
        }

        /// <summary>
        /// Gets the client IP address from the request
        /// </summary>
        private string GetClientIpAddress(Microsoft.AspNetCore.Http.HttpContext? httpContext)
        {
            if (httpContext == null) return "unknown";

            // Check X-Forwarded-For header first (for reverse proxies)
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ip = forwardedFor.Split(',').First().Trim();
                if (System.Net.IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }

            // Check X-Real-IP header
            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && System.Net.IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}