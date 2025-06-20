using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Authentication handler for Virtual Key authentication in the Core API
    /// </summary>
    public class VirtualKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IVirtualKeyService _virtualKeyService;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyAuthenticationHandler
        /// </summary>
        public VirtualKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IVirtualKeyService virtualKeyService)
            : base(options, logger, encoder)
        {
            _virtualKeyService = virtualKeyService;
        }

        /// <summary>
        /// Handles the authentication for the request
        /// </summary>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // Skip authentication for excluded paths
                if (IsPathExcluded(Context.Request.Path))
                {
                    // Create an anonymous identity for excluded paths
                    var anonymousIdentity = new ClaimsIdentity();
                    var anonymousPrincipal = new ClaimsPrincipal(anonymousIdentity);
                    var anonymousTicket = new AuthenticationTicket(anonymousPrincipal, Scheme.Name);
                    return AuthenticateResult.Success(anonymousTicket);
                }

                // Extract Virtual Key from request
                var virtualKey = ExtractVirtualKey(Context);
                
                if (string.IsNullOrEmpty(virtualKey))
                {
                    Logger.LogWarning("Missing Virtual Key in request to {Path} from IP {IP}", 
                        Context.Request.Path, GetClientIpAddress(Context));
                    return AuthenticateResult.Fail("Missing Virtual Key");
                }

                // Validate the Virtual Key
                var keyEntity = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
                if (keyEntity == null)
                {
                    Logger.LogWarning("Invalid Virtual Key in request to {Path} from IP {IP}", 
                        Context.Request.Path, GetClientIpAddress(Context));
                    return AuthenticateResult.Fail("Invalid Virtual Key");
                }

                // Create claims for the authenticated user
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, keyEntity.KeyName ?? "Unknown"),
                    new Claim("VirtualKeyId", keyEntity.Id.ToString()),
                    new Claim("VirtualKey", virtualKey)
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                Logger.LogDebug("Successfully authenticated Virtual Key {KeyName} for {Path}", 
                    keyEntity.KeyName, Context.Request.Path);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during Virtual Key authentication for {Path}", Context.Request.Path);
                return AuthenticateResult.Fail("Authentication error");
            }
        }

        /// <summary>
        /// Determines if the path should be excluded from authentication
        /// </summary>
        private bool IsPathExcluded(string path)
        {
            var excludedPaths = new[]
            {
                "/health",
                "/health/ready",
                "/health/live",
                "/metrics"
            };

            return Array.Exists(excludedPaths, excludedPath => 
                path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Extracts the Virtual Key from the request headers
        /// </summary>
        private string? ExtractVirtualKey(Microsoft.AspNetCore.Http.HttpContext context)
        {
            // Try Authorization header first (Bearer token)
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
        private string GetClientIpAddress(Microsoft.AspNetCore.Http.HttpContext context)
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
}