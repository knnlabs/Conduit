using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Admin.Options;
using ConduitLLM.Admin.Interfaces;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Implementation of unified security service for Admin API
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly SecurityOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecurityService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        // Cache keys - same as WebUI for shared tracking
        private const string RATE_LIMIT_PREFIX = "rate_limit:";
        private const string FAILED_LOGIN_PREFIX = "failed_login:";
        private const string BAN_PREFIX = "ban:";

        // Service identifier for tracking
        private const string SERVICE_NAME = "admin-api";

        /// <summary>
        /// Initializes a new instance of the SecurityService
        /// </summary>
        public SecurityService(
            IOptions<SecurityOptions> options,
            IConfiguration configuration,
            ILogger<SecurityService> logger,
            IMemoryCache memoryCache,
            IDistributedCache? distributedCache,
            IServiceScopeFactory serviceScopeFactory)
        {
            _options = options.Value;
            _configuration = configuration;
            _logger = logger;
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <inheritdoc/>
        public async Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var path = context.Request.Path.Value ?? "";

            // First check API key authentication (unless excluded path)
            if (!IsPathExcluded(path, new List<string> { "/health", "/swagger", "/hubs" }))
            {
                if (!IsApiKeyValid(context))
                {
                    // Record failed auth attempt before returning
                    await RecordFailedAuthAsync(clientIp);
                    
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Reason = "Invalid or missing API key",
                        StatusCode = 401
                    };
                }
            }

            // Check if IP is banned due to failed authentication
            if (await IsIpBannedAsync(clientIp))
            {
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "IP is banned due to excessive failed authentication attempts",
                    StatusCode = 403
                };
            }

            // Check rate limiting (if enabled)
            if (_options.RateLimiting.Enabled && !IsPathExcluded(path, _options.RateLimiting.ExcludedPaths))
            {
                var rateLimitResult = await CheckRateLimitAsync(clientIp);
                if (!rateLimitResult.IsAllowed)
                {
                    return rateLimitResult;
                }
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

        /// <inheritdoc/>
        public bool ValidateApiKey(string providedKey)
        {
            var masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY") 
                           ?? _configuration["AdminApi:MasterKey"];

            return !string.IsNullOrEmpty(masterKey) && providedKey == masterKey;
        }

        private bool IsApiKeyValid(HttpContext context)
        {
            // Check primary header
            if (context.Request.Headers.TryGetValue(_options.ApiAuth.ApiKeyHeader, out var apiKey))
            {
                // Check if it's an ephemeral master key (starts with "emk_")
                if (!string.IsNullOrEmpty(apiKey) && apiKey.ToString().StartsWith("emk_", StringComparison.Ordinal))
                {
                    // Ephemeral keys are validated by the authentication middleware
                    // We just need to confirm it's present and has the right format
                    return true;
                }
                
                if (ValidateApiKey(apiKey!))
                    return true;
            }

            // Check alternative headers for backward compatibility
            foreach (var header in _options.ApiAuth.AlternativeHeaders)
            {
                if (context.Request.Headers.TryGetValue(header, out var altKey))
                {
                    // Check if it's an ephemeral master key (starts with "emk_")
                    if (!string.IsNullOrEmpty(altKey) && altKey.ToString().StartsWith("emk_", StringComparison.Ordinal))
                    {
                        // Ephemeral keys are validated by the authentication middleware
                        // We just need to confirm it's present and has the right format
                        return true;
                    }
                    
                    if (ValidateApiKey(altKey!))
                        return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public async Task RecordFailedAuthAsync(string ipAddress)
        {
#if DEBUG
            // Skip recording failed auth attempts in development mode
            _logger.LogDebug("Failed auth recording is disabled in DEBUG mode for IP {IpAddress}", ipAddress);
            await Task.CompletedTask;
            return;
#else
            // Check if IP banning is enabled via configuration
            if (!_options.FailedAuth.Enabled)
            {
                _logger.LogDebug("Failed auth recording is disabled via configuration for IP {IpAddress}", ipAddress);
                return;
            }
            var key = $"{FAILED_LOGIN_PREFIX}{ipAddress}";
            var banKey = $"{BAN_PREFIX}{ipAddress}";

            // Get current failed attempts
            var attempts = 0;
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    var data = JsonSerializer.Deserialize<FailedAuthData>(cachedValue);
                    attempts = data?.Attempts ?? 0;
                }
            }
            else
            {
                attempts = _memoryCache.Get<int>(key);
            }

            attempts++;

            // Check if we should ban the IP
            if (attempts >= _options.FailedAuth.MaxAttempts)
            {
                var banInfo = new BannedIpInfo
                {
                    BannedUntil = DateTime.UtcNow.AddMinutes(_options.FailedAuth.BanDurationMinutes),
                    FailedAttempts = attempts,
                    Source = SERVICE_NAME,
                    Reason = "Exceeded max failed authentication attempts"
                };

                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.SetStringAsync(
                        banKey,
                        JsonSerializer.Serialize(banInfo),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes)
                        });
                }
                else
                {
                    _memoryCache.Set(banKey, banInfo, TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes));
                }

                _logger.LogWarning("IP {IpAddress} has been banned after {Attempts} failed authentication attempts", 
                    ipAddress, attempts);

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
                var authData = new FailedAuthData
                {
                    Attempts = attempts,
                    Source = SERVICE_NAME,
                    LastAttempt = DateTime.UtcNow
                };

                if (_options.UseDistributedTracking && _distributedCache != null)
                {
                    await _distributedCache.SetStringAsync(
                        key,
                        JsonSerializer.Serialize(authData),
                        new DistributedCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes)
                        });
                }
                else
                {
                    _memoryCache.Set(key, attempts, TimeSpan.FromMinutes(_options.FailedAuth.BanDurationMinutes));
                }

                _logger.LogInformation("Failed authentication attempt {Attempts}/{MaxAttempts} for IP {IpAddress}", 
                    attempts, _options.FailedAuth.MaxAttempts, ipAddress);
            }
#endif
        }

        /// <inheritdoc/>
        public async Task ClearFailedAuthAttemptsAsync(string ipAddress)
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

            _logger.LogInformation("Cleared failed authentication attempts for IP {IpAddress}", ipAddress);
        }

        /// <inheritdoc/>
        public async Task<bool> IsIpBannedAsync(string ipAddress)
        {
#if DEBUG
            // IP banning is disabled in development mode
            _logger.LogDebug("IP banning is disabled in DEBUG mode");
            return await Task.FromResult(false);
#else
            // Check if IP banning is enabled via configuration
            if (!_options.FailedAuth.Enabled)
            {
                _logger.LogDebug("IP banning is disabled via configuration");
                return false;
            }
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
#endif
        }

        private async Task<SecurityCheckResult> CheckRateLimitAsync(string ipAddress)
        {
            var key = $"{RATE_LIMIT_PREFIX}{SERVICE_NAME}:{ipAddress}";
            var now = DateTime.UtcNow;

            // Get current request count
            var requestCount = 0;
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    var data = JsonSerializer.Deserialize<RateLimitData>(cachedValue);
                    requestCount = data?.Count ?? 0;
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
            var rateLimitData = new RateLimitData
            {
                Count = requestCount,
                Source = SERVICE_NAME,
                WindowStart = now
            };

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    JsonSerializer.Serialize(rateLimitData),
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
                if (IsPrivateIp(ipAddress))
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

            // Also check database-based IP filters
            using var scope = _serviceScopeFactory.CreateScope();
            var ipFilterService = scope.ServiceProvider.GetRequiredService<IAdminIpFilterService>();
            var isAllowedByDb = await ipFilterService.IsIpAllowedAsync(ipAddress);
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

        private bool IsPrivateIp(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            // Check loopback
            if (IPAddress.IsLoopback(ip))
                return true;

            // Check private ranges
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var ipBytes = ip.GetAddressBytes();
                
                // Check private ranges
                if (ipBytes[0] == 10 || // 10.0.0.0/8
                    (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) || // 172.16.0.0/12
                    (ipBytes[0] == 192 && ipBytes[1] == 168) || // 192.168.0.0/16
                    (ipBytes[0] == 169 && ipBytes[1] == 254)) // 169.254.0.0/16 (link-local)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsIpInRange(string ipAddress, string rule)
        {
            // Simple IP match
            if (ipAddress == rule)
                return true;

            // CIDR range check
            if (rule.Contains('/'))
            {
                return IsIpInCidrRange(ipAddress, rule);
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

        private bool IsIpInCidrRange(string ipAddress, string cidrRange)
        {
            try
            {
                var parts = cidrRange.Split('/');
                if (parts.Length != 2)
                    return false;

                if (!IPAddress.TryParse(ipAddress, out var ip))
                    return false;

                if (!IPAddress.TryParse(parts[0], out var baseAddress))
                    return false;

                if (!int.TryParse(parts[1], out var prefixLength))
                    return false;

                // Only support IPv4 for now
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                    baseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    return false;

                var ipBytes = ip.GetAddressBytes();
                var baseBytes = baseAddress.GetAddressBytes();

                // Calculate the mask
                var maskBytes = new byte[4];
                for (int i = 0; i < 4; i++)
                {
                    if (prefixLength >= 8)
                    {
                        maskBytes[i] = 0xFF;
                        prefixLength -= 8;
                    }
                    else if (prefixLength > 0)
                    {
                        maskBytes[i] = (byte)(0xFF << (8 - prefixLength));
                        prefixLength = 0;
                    }
                    else
                    {
                        maskBytes[i] = 0x00;
                    }
                }

                // Check if the IP is in the range
                for (int i = 0; i < 4; i++)
                {
                    if ((ipBytes[i] & maskBytes[i]) != (baseBytes[i] & maskBytes[i]))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Data structures for Redis storage (compatible with WebUI)
        private class FailedAuthData
        {
            public int Attempts { get; set; }
            public string Source { get; set; } = "";
            public DateTime LastAttempt { get; set; }
        }

        private class BannedIpInfo
        {
            public DateTime BannedUntil { get; set; }
            public int FailedAttempts { get; set; }
            public string Source { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        private class RateLimitData
        {
            public int Count { get; set; }
            public string Source { get; set; } = "";
            public DateTime WindowStart { get; set; }
        }
    }
}