using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Interfaces;
using System.Text.Json;
using System.Net;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Unified security service that handles all security checks in one place
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Checks if a request is allowed based on all security rules
        /// </summary>
        Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context);

        /// <summary>
        /// Records a failed login attempt
        /// </summary>
        Task RecordFailedLoginAsync(string ipAddress);

        /// <summary>
        /// Clears failed login attempts for an IP
        /// </summary>
        Task ClearFailedLoginAttemptsAsync(string ipAddress);

        /// <summary>
        /// Gets security dashboard data
        /// </summary>
        Task<SecurityDashboardData> GetSecurityDashboardDataAsync();

        /// <summary>
        /// Checks if an IP is banned due to failed logins
        /// </summary>
        Task<bool> IsIpBannedAsync(string ipAddress);

        /// <summary>
        /// Gets the classification of an IP address
        /// </summary>
        IpClassification ClassifyIpAddress(string ipAddress);
    }

    /// <summary>
    /// Result of a security check
    /// </summary>
    public class SecurityCheckResult
    {
        public bool IsAllowed { get; set; }
        public string Reason { get; set; } = "";
        public int? StatusCode { get; set; }
    }

    /// <summary>
    /// Security dashboard data
    /// </summary>
    public class SecurityDashboardData
    {
        public bool IpFilteringEnabled { get; set; }
        public string IpFilterMode { get; set; } = "";
        public List<string> Whitelist { get; set; } = new();
        public List<string> Blacklist { get; set; } = new();
        public bool RateLimitingEnabled { get; set; }
        public int RateLimitMaxRequests { get; set; }
        public int RateLimitWindowSeconds { get; set; }
        public Dictionary<string, BannedIpInfo> BannedIps { get; set; } = new();
        public Dictionary<string, int> RecentFailedAttempts { get; set; } = new();
    }

    /// <summary>
    /// Information about a banned IP
    /// </summary>
    public class BannedIpInfo
    {
        public DateTime BannedUntil { get; set; }
        public int FailedAttempts { get; set; }
    }

    /// <summary>
    /// IP address classification
    /// </summary>
    public enum IpClassification
    {
        Unknown,
        Private,
        Public,
        Loopback,
        LinkLocal
    }

    /// <summary>
    /// Implementation of unified security service
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly SecurityOptions _options;
        private readonly ILogger<SecurityService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly IIpFilterService _ipFilterService;

        // Cache keys
        private const string RATE_LIMIT_PREFIX = "rate_limit:";
        private const string FAILED_LOGIN_PREFIX = "failed_login:";
        private const string BAN_PREFIX = "ban:";

        // Private IP ranges
        private static readonly List<(string Cidr, string Description)> PrivateIpv4Ranges = new()
        {
            ("10.0.0.0/8", "Class A private"),
            ("172.16.0.0/12", "Class B private"),
            ("192.168.0.0/16", "Class C private"),
            ("127.0.0.0/8", "Loopback"),
            ("169.254.0.0/16", "Link-local")
        };

        public SecurityService(
            IOptions<SecurityOptions> options,
            ILogger<SecurityService> logger,
            IMemoryCache memoryCache,
            IDistributedCache? distributedCache,
            IIpFilterService ipFilterService)
        {
            _options = options.Value;
            _logger = logger;
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _ipFilterService = ipFilterService;
        }

        public async Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var path = context.Request.Path.Value ?? "";

            // Check rate limiting first (if enabled)
            if (_options.RateLimiting.Enabled && !IsPathExcluded(path, _options.RateLimiting.ExcludedPaths))
            {
                var rateLimitResult = await CheckRateLimitAsync(clientIp);
                if (!rateLimitResult.IsAllowed)
                {
                    return rateLimitResult;
                }
            }

            // Check if IP is banned due to failed logins
            if (await IsIpBannedAsync(clientIp))
            {
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "IP is banned due to excessive failed login attempts",
                    StatusCode = 403
                };
            }

            // Check IP filtering (if enabled)
            if (_options.IpFiltering.Enabled && !IsPathExcluded(path, _options.IpFiltering.ExcludedPaths))
            {
                var ipFilterResult = await CheckIpFilterAsync(clientIp);
                if (!ipFilterResult.IsAllowed)
                {
                    return ipFilterResult;
                }
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        public async Task RecordFailedLoginAsync(string ipAddress)
        {
            var key = $"{FAILED_LOGIN_PREFIX}{ipAddress}";
            var banKey = $"{BAN_PREFIX}{ipAddress}";

            // Get current failed attempts
            var attempts = 0;
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    attempts = JsonSerializer.Deserialize<int>(cachedValue);
                }
            }
            else
            {
                attempts = _memoryCache.Get<int>(key);
            }

            attempts++;

            // Check if we should ban the IP
            if (attempts >= _options.FailedLogin.MaxAttempts)
            {
                var banInfo = new BannedIpInfo
                {
                    BannedUntil = DateTime.UtcNow.AddMinutes(_options.FailedLogin.BanDurationMinutes),
                    FailedAttempts = attempts
                };

                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.SetStringAsync(
                        banKey,
                        JsonSerializer.Serialize(banInfo),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.FailedLogin.BanDurationMinutes)
                        });
                }
                else
                {
                    _memoryCache.Set(banKey, banInfo, TimeSpan.FromMinutes(_options.FailedLogin.BanDurationMinutes));
                }

                _logger.LogWarning("IP {IpAddress} has been banned after {Attempts} failed login attempts", ipAddress, attempts);

                // Clear the failed attempts counter
                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.RemoveAsync(key);
                }
                else
                {
                    _memoryCache.Remove(key);
                }
            }
            else
            {
                // Update the failed attempts counter
                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.SetStringAsync(
                        key,
                        JsonSerializer.Serialize(attempts),
                        new DistributedCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(_options.FailedLogin.BanDurationMinutes)
                        });
                }
                else
                {
                    _memoryCache.Set(key, attempts, TimeSpan.FromMinutes(_options.FailedLogin.BanDurationMinutes));
                }

                _logger.LogInformation("Failed login attempt {Attempts}/{MaxAttempts} for IP {IpAddress}", 
                    attempts, _options.FailedLogin.MaxAttempts, ipAddress);
            }
        }

        public async Task ClearFailedLoginAttemptsAsync(string ipAddress)
        {
            var key = $"{FAILED_LOGIN_PREFIX}{ipAddress}";

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.RemoveAsync(key);
            }
            else
            {
                _memoryCache.Remove(key);
            }

            _logger.LogInformation("Cleared failed login attempts for IP {IpAddress}", ipAddress);
        }

        public async Task<bool> IsIpBannedAsync(string ipAddress)
        {
            var banKey = $"{BAN_PREFIX}{ipAddress}";

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(banKey);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    var banInfo = JsonSerializer.Deserialize<BannedIpInfo>(cachedValue);
                    return banInfo?.BannedUntil > DateTime.UtcNow;
                }
            }
            else
            {
                var banInfo = _memoryCache.Get<BannedIpInfo>(banKey);
                return banInfo?.BannedUntil > DateTime.UtcNow;
            }

            return false;
        }

        public async Task<SecurityDashboardData> GetSecurityDashboardDataAsync()
        {
            var data = new SecurityDashboardData
            {
                IpFilteringEnabled = _options.IpFiltering.Enabled,
                IpFilterMode = _options.IpFiltering.Mode,
                Whitelist = _options.IpFiltering.Whitelist,
                Blacklist = _options.IpFiltering.Blacklist,
                RateLimitingEnabled = _options.RateLimiting.Enabled,
                RateLimitMaxRequests = _options.RateLimiting.MaxRequests,
                RateLimitWindowSeconds = _options.RateLimiting.WindowSeconds
            };

            // Get banned IPs and failed attempts from cache
            // This is a simplified version - in production you might want to track these separately
            await Task.CompletedTask;

            return data;
        }

        public IpClassification ClassifyIpAddress(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                return IpClassification.Unknown;
            }

            // Check loopback
            if (IPAddress.IsLoopback(ip))
            {
                return IpClassification.Loopback;
            }

            // Check if it's a private IP
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var ipBytes = ip.GetAddressBytes();
                
                // Check private ranges
                if (ipBytes[0] == 10 || // 10.0.0.0/8
                    (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) || // 172.16.0.0/12
                    (ipBytes[0] == 192 && ipBytes[1] == 168) || // 192.168.0.0/16
                    (ipBytes[0] == 169 && ipBytes[1] == 254)) // 169.254.0.0/16 (link-local)
                {
                    return ipBytes[0] == 169 && ipBytes[1] == 254 ? IpClassification.LinkLocal : IpClassification.Private;
                }
            }

            return IpClassification.Public;
        }

        private async Task<SecurityCheckResult> CheckRateLimitAsync(string ipAddress)
        {
            var key = $"{RATE_LIMIT_PREFIX}{ipAddress}";
            var now = DateTime.UtcNow;
            var windowStart = now.AddSeconds(-_options.RateLimiting.WindowSeconds);

            // Get current request count
            var requestCount = 0;
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    requestCount = JsonSerializer.Deserialize<int>(cachedValue);
                }
            }
            else
            {
                requestCount = _memoryCache.Get<int>(key);
            }

            requestCount++;

            if (requestCount > _options.RateLimiting.MaxRequests)
            {
                _logger.LogWarning("Rate limit exceeded for IP {IpAddress}: {Count} requests in {Window} seconds",
                    ipAddress, requestCount, _options.RateLimiting.WindowSeconds);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "Rate limit exceeded",
                    StatusCode = 429
                };
            }

            // Update the counter
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    JsonSerializer.Serialize(requestCount),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.RateLimiting.WindowSeconds)
                    });
            }
            else
            {
                _memoryCache.Set(key, requestCount, TimeSpan.FromSeconds(_options.RateLimiting.WindowSeconds));
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        private async Task<SecurityCheckResult> CheckIpFilterAsync(string ipAddress)
        {
            // Check if it's a private IP and we allow private IPs
            if (_options.IpFiltering.AllowPrivateIps)
            {
                var classification = ClassifyIpAddress(ipAddress);
                if (classification == IpClassification.Private || 
                    classification == IpClassification.Loopback ||
                    classification == IpClassification.LinkLocal)
                {
                    _logger.LogDebug("Private/Intranet IP {IpAddress} is automatically allowed", ipAddress);
                    return new SecurityCheckResult { IsAllowed = true };
                }
            }

            // Check environment variable based filters
            var isInWhitelist = _options.IpFiltering.Whitelist.Any(rule => IsIpInRange(ipAddress, rule));
            var isInBlacklist = _options.IpFiltering.Blacklist.Any(rule => IsIpInRange(ipAddress, rule));

            var isAllowed = _options.IpFiltering.Mode.ToLower() == "restrictive" 
                ? isInWhitelist && !isInBlacklist
                : !isInBlacklist;

            if (!isAllowed)
            {
                _logger.LogWarning("IP {IpAddress} blocked by IP filter rules", ipAddress);
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "IP address not allowed",
                    StatusCode = 403
                };
            }

            // Also check database-based IP filters via the existing service
            var isAllowedByDb = await _ipFilterService.IsIpAllowedAsync(ipAddress);
            if (!isAllowedByDb)
            {
                _logger.LogWarning("IP {IpAddress} blocked by database IP filter", ipAddress);
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "IP address not allowed",
                    StatusCode = 403
                };
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        private bool IsIpInRange(string ipAddress, string rule)
        {
            // Simple IP match
            if (ipAddress == rule)
                return true;

            // CIDR range check
            if (rule.Contains('/'))
            {
                return IpAddressValidator.IsIpInCidrRange(ipAddress, rule);
            }

            return false;
        }

        private bool IsPathExcluded(string path, List<string> excludedPaths)
        {
            return excludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Check X-Forwarded-For header first (for reverse proxies)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP in the chain
                var ip = forwardedFor.Split(',').First().Trim();
                if (IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }

            // Check X-Real-IP header
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}