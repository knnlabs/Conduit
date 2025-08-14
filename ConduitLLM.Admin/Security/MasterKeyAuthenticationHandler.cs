using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.Admin.Services;

namespace ConduitLLM.Admin.Security
{
    /// <summary>
    /// Authentication handler that validates the master key from request headers
    /// </summary>
    public class MasterKeyAuthenticationHandler : AuthenticationHandler<MasterKeyAuthenticationSchemeOptions>
    {
        private readonly string? _masterKey;
        private readonly IEphemeralMasterKeyService _ephemeralMasterKeyService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterKeyAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="options">The monitor for authentication scheme options</param>
        /// <param name="logger">The logger factory</param>
        /// <param name="encoder">The URL encoder</param>
        /// <param name="configuration">The application configuration</param>
        /// <param name="ephemeralMasterKeyService">The ephemeral master key service</param>
        public MasterKeyAuthenticationHandler(
            IOptionsMonitor<MasterKeyAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration,
            IEphemeralMasterKeyService ephemeralMasterKeyService)
            : base(options, logger, encoder)
        {
            // Get backend auth key from environment variable first, then fallback to configuration
            _masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY") 
                ?? configuration["AdminApi:MasterKey"];
            _ephemeralMasterKeyService = ephemeralMasterKeyService ?? throw new ArgumentNullException(nameof(ephemeralMasterKeyService));
        }

        /// <summary>
        /// Handles the authentication by validating the master key from request headers.
        /// </summary>
        /// <returns>The result of the authentication attempt</returns>
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
                return AuthenticateResult.Fail("Missing master key");
            }

            // Check if this is an ephemeral master key
            if (providedKey.StartsWith("emk_", StringComparison.Ordinal))
            {
                // Check if this is a streaming request
                bool isStreaming = Context.Request.Path.StartsWithSegments("/hubs");
                
                bool isValid;
                if (isStreaming)
                {
                    // For streaming, consume and delete immediately
                    isValid = await _ephemeralMasterKeyService.ConsumeKeyAsync(providedKey);
                    
                    if (!isValid)
                    {
                        var keyExists = await _ephemeralMasterKeyService.KeyExistsAsync(providedKey);
                        if (!keyExists)
                        {
                            Logger.LogWarning("Ephemeral master key not found: {Key}", SanitizeKeyForLogging(providedKey));
                            return AuthenticateResult.Fail("Ephemeral master key not found");
                        }
                        
                        Logger.LogWarning("Ephemeral master key already used or expired: {Key}", SanitizeKeyForLogging(providedKey));
                        return AuthenticateResult.Fail("Ephemeral master key already used");
                    }
                }
                else
                {
                    // For non-streaming, validate and mark as consumed (delete happens in middleware)
                    isValid = await _ephemeralMasterKeyService.ValidateAndConsumeKeyAsync(providedKey);
                    
                    if (!isValid)
                    {
                        var keyExists = await _ephemeralMasterKeyService.KeyExistsAsync(providedKey);
                        if (!keyExists)
                        {
                            Logger.LogWarning("Ephemeral master key not found: {Key}", SanitizeKeyForLogging(providedKey));
                            return AuthenticateResult.Fail("Ephemeral master key not found");
                        }
                        
                        Logger.LogWarning("Ephemeral master key validation failed: {Key}", SanitizeKeyForLogging(providedKey));
                        return AuthenticateResult.Fail("Ephemeral master key expired");
                    }
                    
                    // Store for cleanup after request
                    Context.Items["EphemeralMasterKey"] = providedKey;
                    Context.Items["DeleteEphemeralMasterKey"] = true;
                }

                Logger.LogInformation("Authenticated via ephemeral master key");

                // Create authenticated user - same as regular master key auth for transparency
                var emkClaims = new[]
                {
                    new Claim(ClaimTypes.Name, "AdminUser"),
                    new Claim(ClaimTypes.NameIdentifier, "admin"),
                    new Claim("MasterKey", "true")
                };

                var emkIdentity = new ClaimsIdentity(emkClaims, Scheme.Name);
                var emkPrincipal = new ClaimsPrincipal(emkIdentity);
                var emkTicket = new AuthenticationTicket(emkPrincipal, Scheme.Name);

                return AuthenticateResult.Success(emkTicket);
            }

            // Regular master key validation
            if (string.IsNullOrEmpty(_masterKey))
            {
                Logger.LogError("Backend auth key is not configured. Set CONDUIT_API_TO_API_BACKEND_AUTH_KEY environment variable.");
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

        private static string SanitizeKeyForLogging(string key)
        {
            // Only show first 10 characters of the key for security
            if (key.Length <= 10)
                return key;
                
            return $"{key.Substring(0, 10)}...";
        }
    }

    /// <summary>
    /// Options for the master key authentication scheme
    /// </summary>
    public class MasterKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
    }
}