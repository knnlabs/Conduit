using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for tracking failed login attempts and implementing IP-based banning
    /// </summary>
    public interface IFailedLoginTrackingService
    {
        /// <summary>
        /// Records a failed login attempt for an IP address
        /// </summary>
        void RecordFailedLogin(string ipAddress);

        /// <summary>
        /// Clears failed login attempts for an IP address
        /// </summary>
        void ClearFailedAttempts(string ipAddress);

        /// <summary>
        /// Checks if an IP address is currently banned
        /// </summary>
        bool IsBanned(string ipAddress);
    }

    /// <summary>
    /// Implementation of failed login tracking service
    /// </summary>
    public class FailedLoginTrackingService : IFailedLoginTrackingService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ISecurityConfigurationService _securityConfig;
        private readonly ILogger<FailedLoginTrackingService> _logger;

        private const string FAILED_ATTEMPTS_PREFIX = "failed_attempts_";
        private const string IP_BAN_PREFIX = "ip_ban_";

        public FailedLoginTrackingService(
            IMemoryCache memoryCache,
            ISecurityConfigurationService securityConfig,
            ILogger<FailedLoginTrackingService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void RecordFailedLogin(string ipAddress)
        {
            var cacheKey = $"{FAILED_ATTEMPTS_PREFIX}{ipAddress}";
            var attempts = _memoryCache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(_securityConfig.IpBanDurationMinutes);
                return 0;
            });

            attempts++;
            _memoryCache.Set(cacheKey, attempts, TimeSpan.FromMinutes(_securityConfig.IpBanDurationMinutes));

            var maxAttempts = _securityConfig.MaxFailedLoginAttempts;
            _logger.LogWarning("Failed login attempt {Attempts}/{MaxAttempts} from IP: {IpAddress}", 
                attempts, maxAttempts, ipAddress);

            if (attempts >= maxAttempts)
            {
                BanIpAddress(ipAddress);
            }
        }

        /// <inheritdoc />
        public void ClearFailedAttempts(string ipAddress)
        {
            var cacheKey = $"{FAILED_ATTEMPTS_PREFIX}{ipAddress}";
            _memoryCache.Remove(cacheKey);
            _logger.LogInformation("Cleared failed login attempts for IP: {IpAddress}", ipAddress);
        }

        /// <inheritdoc />
        public bool IsBanned(string ipAddress)
        {
            var banKey = $"{IP_BAN_PREFIX}{ipAddress}";
            return _memoryCache.TryGetValue(banKey, out _);
        }

        private void BanIpAddress(string ipAddress)
        {
            var banKey = $"{IP_BAN_PREFIX}{ipAddress}";
            var banDuration = _securityConfig.IpBanDurationMinutes;
            _memoryCache.Set(banKey, true, TimeSpan.FromMinutes(banDuration));
            _logger.LogWarning("Banned IP address {IpAddress} for {Duration} minutes", ipAddress, banDuration);
        }
    }
}