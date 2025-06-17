using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Distributed implementation of failed login tracking service using Redis
/// </summary>
public class DistributedFailedLoginTrackingService : IFailedLoginTrackingService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ISecurityConfigurationService _securityConfig;
    private readonly ILogger<DistributedFailedLoginTrackingService> _logger;

    private const string FAILED_ATTEMPTS_PREFIX = "conduit:security:failed_attempts:";
    private const string IP_BAN_PREFIX = "conduit:security:ip_ban:";

    public DistributedFailedLoginTrackingService(
        IDistributedCache distributedCache,
        ISecurityConfigurationService securityConfig,
        ILogger<DistributedFailedLoginTrackingService> logger)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void RecordFailedLogin(string ipAddress)
    {
        Task.Run(async () => await RecordFailedLoginAsync(ipAddress)).Wait();
    }

    /// <summary>
    /// Async version of RecordFailedLogin for better Redis integration
    /// </summary>
    public async Task RecordFailedLoginAsync(string ipAddress)
    {
        var cacheKey = $"{FAILED_ATTEMPTS_PREFIX}{ipAddress}";
        var banDurationMinutes = _securityConfig.IpBanDurationMinutes;
        
        try
        {
            // Get current attempts
            var attemptsData = await _distributedCache.GetStringAsync(cacheKey);
            var attempts = 0;
            
            if (!string.IsNullOrEmpty(attemptsData))
            {
                if (int.TryParse(attemptsData, out var existingAttempts))
                {
                    attempts = existingAttempts;
                }
            }

            attempts++;

            // Store updated attempts with sliding expiration
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(banDurationMinutes)
            };
            
            await _distributedCache.SetStringAsync(cacheKey, attempts.ToString(), options);

            var maxAttempts = _securityConfig.MaxFailedLoginAttempts;
            _logger.LogWarning("Failed login attempt {Attempts}/{MaxAttempts} from IP: {IpAddress}", 
                attempts, maxAttempts, ipAddress);

            if (attempts >= maxAttempts)
            {
                await BanIpAddressAsync(ipAddress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording failed login for IP {IpAddress}", ipAddress);
        }
    }

    /// <inheritdoc />
    public void ClearFailedAttempts(string ipAddress)
    {
        Task.Run(async () => await ClearFailedAttemptsAsync(ipAddress)).Wait();
    }

    /// <summary>
    /// Async version of ClearFailedAttempts
    /// </summary>
    public async Task ClearFailedAttemptsAsync(string ipAddress)
    {
        var cacheKey = $"{FAILED_ATTEMPTS_PREFIX}{ipAddress}";
        
        try
        {
            await _distributedCache.RemoveAsync(cacheKey);
            _logger.LogInformation("Cleared failed login attempts for IP: {IpAddress}", ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing failed attempts for IP {IpAddress}", ipAddress);
        }
    }

    /// <inheritdoc />
    public bool IsBanned(string ipAddress)
    {
        return Task.Run(async () => await IsBannedAsync(ipAddress)).Result;
    }

    /// <summary>
    /// Async version of IsBanned
    /// </summary>
    public async Task<bool> IsBannedAsync(string ipAddress)
    {
        var banKey = $"{IP_BAN_PREFIX}{ipAddress}";
        
        try
        {
            var banData = await _distributedCache.GetStringAsync(banKey);
            return !string.IsNullOrEmpty(banData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking ban status for IP {IpAddress}", ipAddress);
            return false; // Fail open to avoid blocking legitimate users
        }
    }

    private async Task BanIpAddressAsync(string ipAddress)
    {
        var banKey = $"{IP_BAN_PREFIX}{ipAddress}";
        var banDuration = _securityConfig.IpBanDurationMinutes;
        
        try
        {
            var banInfo = new BanInfo
            {
                IpAddress = ipAddress,
                BannedAt = DateTime.UtcNow,
                BanDurationMinutes = banDuration,
                Reason = "Exceeded maximum failed login attempts"
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(banDuration)
            };

            await _distributedCache.SetStringAsync(banKey, JsonSerializer.Serialize(banInfo), options);
            _logger.LogWarning("Banned IP address {IpAddress} for {Duration} minutes", ipAddress, banDuration);
            
            // Also clear the failed attempts to reset counter
            await ClearFailedAttemptsAsync(ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error banning IP {IpAddress}", ipAddress);
        }
    }

    private class BanInfo
    {
        public string IpAddress { get; set; } = "";
        public DateTime BannedAt { get; set; }
        public int BanDurationMinutes { get; set; }
        public string Reason { get; set; } = "";
    }
}