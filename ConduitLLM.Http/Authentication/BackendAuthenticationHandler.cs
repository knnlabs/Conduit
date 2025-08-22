using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Authentication handler for service-to-service backend authentication
    /// </summary>
    public class BackendAuthenticationHandler : AuthenticationHandler<BackendAuthenticationSchemeOptions>
    {
        private readonly string? _backendAuthKey;

        public BackendAuthenticationHandler(
            IOptionsMonitor<BackendAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            // Get backend auth key from environment variable first, then fallback to configuration
            _backendAuthKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY") 
                ?? configuration["ConduitLLM:BackendAuthKey"];
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check if backend auth key is configured
            if (string.IsNullOrEmpty(_backendAuthKey))
            {
                Logger.LogWarning("Backend authentication key is not configured");
                return Task.FromResult(AuthenticateResult.Fail("Backend authentication not configured"));
            }

            // Check for the Authorization header
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
            }

            // Extract the auth key from the header
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header format"));
            }

            var providedKey = authHeader.Substring("Bearer ".Length).Trim();

            // Validate the key
            if (providedKey != _backendAuthKey)
            {
                Logger.LogWarning("Invalid backend authentication key provided");
                return Task.FromResult(AuthenticateResult.Fail("Invalid authentication key"));
            }

            // Create claims for the authenticated backend service
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "BackendService"),
                new Claim(ClaimTypes.NameIdentifier, "backend-service"),
                new Claim("AuthType", "Backend")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class BackendAuthenticationSchemeOptions : AuthenticationSchemeOptions { }
}