using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.Http.Services;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Authentication handler for ephemeral API keys
    /// </summary>
    public class EphemeralKeyAuthenticationHandler : AuthenticationHandler<EphemeralKeyAuthenticationOptions>
    {
        private readonly IEphemeralKeyService _ephemeralKeyService;
        private readonly IVirtualKeyService _virtualKeyService;

        public EphemeralKeyAuthenticationHandler(
            IOptionsMonitor<EphemeralKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IEphemeralKeyService ephemeralKeyService,
            IVirtualKeyService virtualKeyService)
            : base(options, logger, encoder)
        {
            _ephemeralKeyService = ephemeralKeyService ?? throw new ArgumentNullException(nameof(ephemeralKeyService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check for ephemeral key in X-Ephemeral-Key header
            if (!Request.Headers.ContainsKey("X-Ephemeral-Key"))
            {
                return AuthenticateResult.NoResult();
            }

            var ephemeralKey = Request.Headers["X-Ephemeral-Key"].ToString();
            
            if (string.IsNullOrEmpty(ephemeralKey))
            {
                Logger.LogWarning("Empty ephemeral key provided in X-Ephemeral-Key header");
                return AuthenticateResult.Fail("Invalid ephemeral key");
            }

            // Check if this is a streaming request
            bool isStreaming = IsStreamingRequest();
            
            int? virtualKeyId;
            
            if (isStreaming)
            {
                // For streaming, consume and delete immediately
                virtualKeyId = await _ephemeralKeyService.ConsumeKeyAsync(ephemeralKey);
                
                if (!virtualKeyId.HasValue)
                {
                    var keyExists = await _ephemeralKeyService.KeyExistsAsync(ephemeralKey);
                    if (!keyExists)
                    {
                        Logger.LogWarning("Ephemeral key not found: {Key}", SanitizeKeyForLogging(ephemeralKey));
                        return AuthenticateResult.Fail("Ephemeral key not found");
                    }
                    
                    Logger.LogWarning("Ephemeral key already used or expired: {Key}", SanitizeKeyForLogging(ephemeralKey));
                    return AuthenticateResult.Fail("Ephemeral key already used");
                }
            }
            else
            {
                // For non-streaming, validate and mark as consumed (delete happens in middleware)
                virtualKeyId = await _ephemeralKeyService.ValidateAndConsumeKeyAsync(ephemeralKey);
                
                if (!virtualKeyId.HasValue)
                {
                    var keyExists = await _ephemeralKeyService.KeyExistsAsync(ephemeralKey);
                    if (!keyExists)
                    {
                        Logger.LogWarning("Ephemeral key not found: {Key}", SanitizeKeyForLogging(ephemeralKey));
                        return AuthenticateResult.Fail("Ephemeral key not found");
                    }
                    
                    Logger.LogWarning("Ephemeral key validation failed: {Key}", SanitizeKeyForLogging(ephemeralKey));
                    return AuthenticateResult.Fail("Ephemeral key expired");
                }
                
                // Store for cleanup after request
                Context.Items["EphemeralKey"] = ephemeralKey;
                Context.Items["DeleteEphemeralKey"] = true;
            }

            // Retrieve the actual virtual key from the ephemeral key data
            var actualVirtualKey = await _ephemeralKeyService.GetVirtualKeyAsync(ephemeralKey);
            if (string.IsNullOrEmpty(actualVirtualKey))
            {
                Logger.LogError("Failed to retrieve virtual key for ephemeral key");
                return AuthenticateResult.Fail("Failed to retrieve associated virtual key");
            }

            // Get the virtual key details
            Logger.LogInformation("Looking up virtual key {VirtualKeyId} for ephemeral key validation", virtualKeyId.Value);
            var virtualKeyInfo = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(virtualKeyId.Value);
            if (virtualKeyInfo == null)
            {
                Logger.LogError("Virtual key {VirtualKeyId} not found for ephemeral key. This likely means the virtual key exists but has no hash stored.", virtualKeyId.Value);
                // Try to get basic info for debugging
                var basicInfo = await _virtualKeyService.GetVirtualKeyInfoAsync(virtualKeyId.Value);
                if (basicInfo != null)
                {
                    Logger.LogWarning("Virtual key {VirtualKeyId} exists with name '{KeyName}' but validation failed - likely missing hash", 
                        virtualKeyId.Value, basicInfo.KeyName);
                }
                return AuthenticateResult.Fail("Associated virtual key not found");
            }

            // Check if virtual key is active
            if (!virtualKeyInfo.IsEnabled)
            {
                Logger.LogWarning("Inactive virtual key {VirtualKeyId} used with ephemeral key", virtualKeyId.Value);
                return AuthenticateResult.Fail("Associated virtual key is inactive");
            }

            // Store the REAL virtual key in context - this makes ephemeral keys transparent to the rest of the system
            Context.Items["VirtualKey"] = actualVirtualKey; // Use the real virtual key
            Context.Items["VirtualKeyId"] = virtualKeyInfo.Id;
            Context.Items["VirtualKeyName"] = virtualKeyInfo.KeyName;
            Context.Items["AuthType"] = "VirtualKey"; // Pretend this is regular virtual key auth
            Context.Items["IsEphemeralKey"] = true; // Keep this for logging/auditing purposes

            // Create claims that match regular virtual key authentication
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, virtualKeyInfo.Id.ToString()),
                new Claim("VirtualKeyId", virtualKeyInfo.Id.ToString()),
                new Claim("VirtualKeyName", virtualKeyInfo.KeyName),
                new Claim("AuthType", "VirtualKey"), // Make it look like regular virtual key auth
                // Use the REAL virtual key in the claim
                new Claim("VirtualKey", actualVirtualKey),
                // VirtualKey doesn't have IsAdmin, so we set it to false for ephemeral keys
                new Claim("IsAdmin", "false"),
                // VirtualKey doesn't have Permissions field directly
                new Claim("Permissions", string.Empty)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogInformation("Ephemeral key authenticated for virtual key {VirtualKeyId} ({VirtualKeyName}), streaming: {IsStreaming}", 
                virtualKeyInfo.Id, virtualKeyInfo.KeyName, isStreaming);

            return AuthenticateResult.Success(ticket);
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers["WWW-Authenticate"] = "EphemeralKey";
            return Task.CompletedTask;
        }

        private bool IsStreamingRequest()
        {
            // Check if this is a streaming request based on path and query parameters
            var path = Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            
            // Check for streaming endpoints
            if (path.Contains("/chat/completions"))
            {
                // Check if stream parameter is set to true
                if (Request.Method == "POST" && Request.ContentType?.Contains("application/json") == true)
                {
                    // We'll check the stream parameter in the request body later
                    // For now, we can check Accept header for SSE
                    return Request.Headers["Accept"].ToString().Contains("text/event-stream");
                }
            }
            
            // Check for other streaming endpoints
            return path.Contains("/stream") || 
                   Request.Headers["Accept"].ToString().Contains("text/event-stream");
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
    /// Options for ephemeral key authentication
    /// </summary>
    public class EphemeralKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        /// Gets or sets whether to allow ephemeral keys
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}