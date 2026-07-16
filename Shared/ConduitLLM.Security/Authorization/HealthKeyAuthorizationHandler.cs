using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Security.Authorization;

/// <summary>
/// Authorization requirement for health endpoint access.
/// </summary>
public class HealthKeyRequirement : IAuthorizationRequirement { }

/// <summary>
/// Authorization handler that allows health endpoint access from private networks
/// or when a valid health monitoring key is provided via the X-Conduit-Health-Key header.
/// </summary>
/// <remarks>
/// This handler implements a tiered security model:
/// <list type="bullet">
/// <item>Private network requests (10.x, 172.16-31.x, 192.168.x, 127.x) are always allowed</item>
/// <item>External requests require the CONDUIT_HEALTH_MONITORING_KEY via X-Conduit-Health-Key header</item>
/// </list>
/// </remarks>
public class HealthKeyAuthorizationHandler : AuthorizationHandler<HealthKeyRequirement>
{
    private readonly string? _healthKey;
    private readonly ILogger<HealthKeyAuthorizationHandler> _logger;

    /// <summary>
    /// Header name for the health monitoring key.
    /// </summary>
    public const string HealthKeyHeaderName = "X-Conduit-Health-Key";

    /// <summary>
    /// Environment variable name for the health monitoring key.
    /// </summary>
    public const string HealthKeyEnvVar = "CONDUIT_HEALTH_MONITORING_KEY";

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthKeyAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public HealthKeyAuthorizationHandler(ILogger<HealthKeyAuthorizationHandler> logger)
    {
        _healthKey = Environment.GetEnvironmentVariable(HealthKeyEnvVar);
        _logger = logger;

        if (string.IsNullOrEmpty(_healthKey))
        {
            _logger.LogWarning(
                "Health monitoring key ({EnvVar}) is not configured. " +
                "External health endpoint access will be denied unless requests come from private networks.",
                HealthKeyEnvVar);
        }
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HealthKeyRequirement requirement)
    {
        var httpContext = context.Resource as HttpContext;
        if (httpContext == null)
        {
            _logger.LogDebug("Authorization context does not contain HttpContext, cannot evaluate health key requirement");
            return Task.CompletedTask;
        }

        // Private network requests are always allowed
        if (IpAddressHelper.IsPrivateNetworkRequest(httpContext))
        {
            _logger.LogDebug(
                "Health endpoint access granted for private network request from {RemoteIp}",
                httpContext.Connection.RemoteIpAddress);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // External requests: check for valid health key header
        if (!string.IsNullOrEmpty(_healthKey) &&
            httpContext.Request.Headers.TryGetValue(HealthKeyHeaderName, out var providedKey) &&
            !string.IsNullOrEmpty(providedKey) &&
            string.Equals(providedKey, _healthKey, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "Health endpoint access granted for external request from {RemoteIp} with valid key",
                httpContext.Connection.RemoteIpAddress);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // If we reach here, authorization fails (handler doesn't call Fail, just doesn't Succeed)
        _logger.LogDebug(
            "Health endpoint access denied for external request from {RemoteIp}: no valid key provided",
            httpContext.Connection.RemoteIpAddress);

        return Task.CompletedTask;
    }
}
