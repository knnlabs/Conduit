using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Unified security service that integrates IP filtering with failed login tracking
/// </summary>
public interface IUnifiedSecurityService
{
    /// <summary>
    /// Checks if an IP address is allowed access considering both IP filters and failed login bans
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>True if allowed, false if blocked</returns>
    Task<bool> IsIpAllowedAsync(string ipAddress);

    /// <summary>
    /// Records a failed login attempt and potentially bans the IP
    /// </summary>
    /// <param name="ipAddress">The IP address that failed login</param>
    Task RecordFailedLoginAsync(string ipAddress);

    /// <summary>
    /// Clears failed login attempts for an IP address
    /// </summary>
    /// <param name="ipAddress">The IP address to clear</param>
    Task ClearFailedAttemptsAsync(string ipAddress);
}

/// <summary>
/// Implementation of unified security service
/// </summary>
public class UnifiedSecurityService : IUnifiedSecurityService
{
    private readonly IIpFilterService _ipFilterService;
    private readonly IFailedLoginTrackingService _failedLoginTracking;
    private readonly IIpAddressClassifier _ipClassifier;
    private readonly ISecurityConfigurationService _securityConfig;
    private readonly ILogger<UnifiedSecurityService> _logger;

    public UnifiedSecurityService(
        IIpFilterService ipFilterService,
        IFailedLoginTrackingService failedLoginTracking,
        IIpAddressClassifier ipClassifier,
        ISecurityConfigurationService securityConfig,
        ILogger<UnifiedSecurityService> logger)
    {
        _ipFilterService = ipFilterService ?? throw new ArgumentNullException(nameof(ipFilterService));
        _failedLoginTracking = failedLoginTracking ?? throw new ArgumentNullException(nameof(failedLoginTracking));
        _ipClassifier = ipClassifier ?? throw new ArgumentNullException(nameof(ipClassifier));
        _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> IsIpAllowedAsync(string ipAddress)
    {
        // First check if IP is banned due to failed logins
        if (_failedLoginTracking.IsBanned(ipAddress))
        {
            _logger.LogWarning("IP {IpAddress} is banned due to failed login attempts", ipAddress);
            return false;
        }

        // Check if private IPs are allowed and this is a private IP
        if (_securityConfig.AllowPrivateIps && _ipClassifier.IsPrivateOrIntranet(ipAddress))
        {
            _logger.LogDebug("Private/Intranet IP {IpAddress} is automatically allowed", ipAddress);
            return true;
        }

        // Check IP filters
        try
        {
            var allowed = await _ipFilterService.IsIpAllowedAsync(ipAddress);
            _logger.LogDebug("IP {IpAddress} filter check result: {Allowed}", ipAddress, allowed);
            return allowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking IP filters for {IpAddress}, allowing access", ipAddress);
            return true; // Allow on error to avoid blocking legitimate access
        }
    }

    /// <inheritdoc />
    public Task RecordFailedLoginAsync(string ipAddress)
    {
        _failedLoginTracking.RecordFailedLogin(ipAddress);
        
        // Log the IP classification for monitoring
        var classification = _ipClassifier.GetClassification(ipAddress);
        _logger.LogWarning("Failed login from {IpAddress} (Type: {Classification})", ipAddress, classification);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearFailedAttemptsAsync(string ipAddress)
    {
        _failedLoginTracking.ClearFailedAttempts(ipAddress);
        return Task.CompletedTask;
    }
}