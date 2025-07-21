using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Security
{
    /// <summary>
    /// Authentication handler that validates the master key from request headers
    /// </summary>
    public class MasterKeyAuthenticationHandler : AuthenticationHandler<MasterKeyAuthenticationSchemeOptions>
    {
        private readonly string? _masterKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterKeyAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="options">The monitor for authentication scheme options</param>
        /// <param name="logger">The logger factory</param>
        /// <param name="encoder">The URL encoder</param>
        /// <param name="configuration">The application configuration</param>
        public MasterKeyAuthenticationHandler(
            IOptionsMonitor<MasterKeyAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            // Get backend auth key from environment variable first, then fallback to configuration
            _masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY") 
                ?? configuration["AdminApi:MasterKey"];
        }

        /// <summary>
        /// Handles the authentication by validating the master key from request headers.
        /// </summary>
        /// <returns>The result of the authentication attempt</returns>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Allow health check endpoints without authentication
            if (Context.Request.Path.StartsWithSegments("/health/live") || 
                Context.Request.Path.StartsWithSegments("/health/ready") ||
                Context.Request.Path.Value == "/health")
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "HealthCheck"),
                    new Claim(ClaimTypes.NameIdentifier, "health-check")
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            // Check for master key in headers and query string
            string? providedKey = null;
            
            // For SignalR hub requests, prioritize query string authentication
            if (Context.Request.Path.StartsWithSegments("/hubs") && 
                Context.Request.Query.TryGetValue("access_token", out var tokenValues))
            {
                providedKey = tokenValues.FirstOrDefault();
                
                // Log when query string auth is used for SignalR
                if (!string.IsNullOrEmpty(providedKey))
                {
                    Logger.LogDebug("Using query string authentication for SignalR hub: {Path}", 
                        Context.Request.Path.ToString().Replace(Environment.NewLine, ""));
                }
            }
            // If not a hub request or no query string token, check headers
            else if (Context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
            {
                providedKey = apiKeyValues.FirstOrDefault();
            }
            else if (Context.Request.Headers.TryGetValue("X-Master-Key", out var masterKeyValues))
            {
                providedKey = masterKeyValues.FirstOrDefault();
            }
            // Check Authorization header for Bearer token (SignalR support)
            else if (Context.Request.Headers.TryGetValue("Authorization", out var authValues))
            {
                var authHeader = authValues.FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                {
                    providedKey = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(providedKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing master key"));
            }

            if (string.IsNullOrEmpty(_masterKey))
            {
                Logger.LogError("Backend auth key is not configured. Set CONDUIT_API_TO_API_BACKEND_AUTH_KEY environment variable.");
                return Task.FromResult(AuthenticateResult.Fail("Master key not configured"));
            }

            if (providedKey != _masterKey)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid master key"));
            }

            // Create authenticated user
            var authClaims = new[]
            {
                new Claim(ClaimTypes.Name, "AdminUser"),
                new Claim(ClaimTypes.NameIdentifier, "admin"),
                new Claim("MasterKey", "true")
            };

            var authIdentity = new ClaimsIdentity(authClaims, Scheme.Name);
            var authPrincipal = new ClaimsPrincipal(authIdentity);
            var authTicket = new AuthenticationTicket(authPrincipal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(authTicket));
        }
    }

    /// <summary>
    /// Options for the master key authentication scheme
    /// </summary>
    public class MasterKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
    }
}