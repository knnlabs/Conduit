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

        public MasterKeyAuthenticationHandler(
            IOptionsMonitor<MasterKeyAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IConfiguration configuration)
            : base(options, logger, encoder, clock)
        {
            // Get master key from environment variable first, then fallback to configuration
            _masterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY") 
                ?? configuration["AdminApi:MasterKey"];
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
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

                return AuthenticateResult.Success(ticket);
            }

            // Check for master key in headers
            string? providedKey = null;
            
            if (Context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
            {
                providedKey = apiKeyValues.FirstOrDefault();
            }
            else if (Context.Request.Headers.TryGetValue("X-Master-Key", out var masterKeyValues))
            {
                providedKey = masterKeyValues.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(providedKey))
            {
                return AuthenticateResult.Fail("Missing master key");
            }

            if (string.IsNullOrEmpty(_masterKey))
            {
                Logger.LogError("Master key is not configured. Set CONDUIT_MASTER_KEY environment variable.");
                return AuthenticateResult.Fail("Master key not configured");
            }

            if (providedKey != _masterKey)
            {
                return AuthenticateResult.Fail("Invalid master key");
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

            return AuthenticateResult.Success(authTicket);
        }
    }

    /// <summary>
    /// Options for the master key authentication scheme
    /// </summary>
    public class MasterKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
    }
}