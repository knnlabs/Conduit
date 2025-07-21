using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace ConduitLLM.Admin.Security;

/// <summary>
/// Authorization handler for validating master key authentication
/// </summary>
public class MasterKeyAuthorizationHandler : AuthorizationHandler<MasterKeyRequirement>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MasterKeyAuthorizationHandler> _logger;
    private const string MASTER_KEY_CONFIG_KEY = "AdminApi:MasterKey";
    private const string MASTER_KEY_HEADER = "X-API-Key";

    /// <summary>
    /// Initializes a new instance of the MasterKeyAuthorizationHandler class
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger</param>
    public MasterKeyAuthorizationHandler(
        IConfiguration configuration,
        ILogger<MasterKeyAuthorizationHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Handles the authorization requirement
    /// </summary>
    /// <param name="context">The authorization context</param>
    /// <param name="requirement">The requirement to be validated</param>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MasterKeyRequirement requirement)
    {
        try
        {
            if (context.Resource is HttpContext httpContext)
            {
                // Get the configured master key
                // Check for CONDUIT_API_TO_API_BACKEND_AUTH_KEY first (new standard), then fall back to AdminApi:MasterKey
                string? masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY") 
                                   ?? _configuration[MASTER_KEY_CONFIG_KEY];

                if (string.IsNullOrEmpty(masterKey))
                {
                    _logger.LogWarning("Master key is not configured");
                    return Task.CompletedTask;
                }

                // Check for X-API-Key header first (preferred)
                if (httpContext.Request.Headers.TryGetValue(MASTER_KEY_HEADER, out var providedKey))
                {
                    // Check if the provided key matches the master key
                    if (providedKey.ToString() == masterKey)
                    {
                        context.Succeed(requirement);
                        return Task.CompletedTask;
                    }
                }

                // Fallback: Check for X-Master-Key header for backward compatibility
                if (httpContext.Request.Headers.TryGetValue("X-Master-Key", out var legacyKey))
                {
                    if (legacyKey.ToString() == masterKey)
                    {
                        context.Succeed(requirement);
                        return Task.CompletedTask;
                    }
                }

                // Check Authorization header for Bearer token (SignalR support)
                if (httpContext.Request.Headers.TryGetValue("Authorization", out var authValues))
                {
                    var authHeader = authValues.FirstOrDefault();
                    if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var bearerToken = authHeader.Substring("Bearer ".Length).Trim();
                        if (bearerToken == masterKey)
                        {
                            context.Succeed(requirement);
                            return Task.CompletedTask;
                        }
                    }
                }

                // Check query string for SignalR WebSocket connections
                if (httpContext.Request.Query.TryGetValue("access_token", out var tokenValues))
                {
                    var queryToken = tokenValues.FirstOrDefault();
                    if (queryToken == masterKey)
                    {
                        // Log when query string auth is used for SignalR
                        if (httpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            _logger.LogDebug("Authorized SignalR hub connection via query string: {Path}", 
                                httpContext.Request.Path.ToString().Replace(Environment.NewLine, ""));
                        }
                        context.Succeed(requirement);
                        return Task.CompletedTask;
                    }
                }

_logger.LogWarning("Invalid master key provided for {Path}", httpContext.Request.Path.ToString().Replace(Environment.NewLine, ""));
            }
            else
            {
                _logger.LogWarning("No HttpContext available for MasterKeyAuthorization");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MasterKeyAuthorizationHandler");
        }

        return Task.CompletedTask;
    }
}
