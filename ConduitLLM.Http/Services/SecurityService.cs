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
using ConduitLLM.Http.Options;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Unified security service for Core API
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Checks if a request is allowed based on all security rules
        /// </summary>
        Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context);

        /// <summary>
        /// Records a failed authentication attempt for an IP
        /// </summary>
        Task RecordFailedAuthAsync(string ipAddress, string attemptedKey);

        /// <summary>
        /// Clears failed authentication attempts for an IP
        /// </summary>
        Task ClearFailedAuthAttemptsAsync(string ipAddress);

        /// <summary>
        /// Checks if an IP is banned due to failed authentication
        /// </summary>
        Task<bool> IsIpBannedAsync(string ipAddress);

        /// <summary>
        /// Checks Virtual Key rate limits
        /// </summary>
        Task<RateLimitCheckResult> CheckVirtualKeyRateLimitAsync(HttpContext context, string virtualKeyId, string endpoint);
    }

    /// <summary>
    /// Result of a security check
    /// </summary>
    public class SecurityCheckResult
    {
        /// <summary>
        /// Whether the request is allowed
        /// </summary>
        public bool IsAllowed { get; set; }
        
        /// <summary>
        /// Reason for denial if not allowed
        /// </summary>
        public string Reason { get; set; } = "";
        
        /// <summary>
        /// HTTP status code to return
        /// </summary>
        public int? StatusCode { get; set; }
        
        /// <summary>
        /// Additional headers to include in response
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    /// <summary>
    /// Result of a rate limit check
    /// </summary>
    public class RateLimitCheckResult
    {
        /// <summary>
        /// Whether the request is allowed
        /// </summary>
        public bool IsAllowed { get; set; }
        
        /// <summary>
        /// Requests remaining in current window
        /// </summary>
        public int? Remaining { get; set; }
        
        /// <summary>
        /// Total limit for the window
        /// </summary>
        public int? Limit { get; set; }
        
        /// <summary>
        /// Window reset time
        /// </summary>
        public DateTime? ResetsAt { get; set; }
    }

    /// <summary>
    /// Implementation of unified security service for Core API
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly SecurityOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecurityService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly IIpFilterService _ipFilterService;

        // Cache keys - same as WebUI/Admin for shared tracking
        private const string RATE_LIMIT_PREFIX = "rate_limit:";
        private const string FAILED_LOGIN_PREFIX = "failed_login:";
        private const string BAN_PREFIX = "ban:";
        private const string VKEY_RATE_LIMIT_PREFIX = "vkey_rate:";

        // Service identifier for tracking
        private const string SERVICE_NAME = "core-api";

        /// <summary>
        /// Initializes a new instance of the SecurityService
        /// </summary>
        public SecurityService(
            IOptions<SecurityOptions> options,
            IConfiguration configuration,
            ILogger<SecurityService> logger,
            IMemoryCache memoryCache,
            IIpFilterService ipFilterService,
            IServiceProvider serviceProvider)
        {
            _options = options.Value;
            _configuration = configuration;
            _logger = logger;
            _memoryCache = memoryCache;
            _distributedCache = serviceProvider.GetService<IDistributedCache>();
            _ipFilterService = ipFilterService;
        }

        /// <inheritdoc/>
        public async Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var path = context.Request.Path.Value ?? "";

            // Skip security checks for excluded paths
            if (IsPathExcluded(path, new List<string> { "/health", "/metrics" }))
            {
                return new SecurityCheckResult { IsAllowed = true };
            }

            // Check if authentication failed (set by VirtualKeyAuthenticationMiddleware)
            if (context.Items.ContainsKey("FailedAuth") && context.Items["FailedAuth"] is bool failedAuth && failedAuth)
            {
                // Record the failed attempt
                var attemptedKey = context.Items["AttemptedKey"] as string ?? "unknown";
                await RecordFailedAuthAsync(clientIp, attemptedKey);
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

            // If authentication succeeded, clear failed attempts for this IP
            if (context.Items.ContainsKey("AuthSuccess") && context.Items["AuthSuccess"] is bool authSuccess && authSuccess)
            {
                await ClearFailedAuthAttemptsAsync(clientIp);
            }

            // Check IP-based rate limiting (if enabled)
            if (_options.RateLimiting.Enabled && !IsPathExcluded(path, _options.RateLimiting.ExcludedPaths))
            {
                var rateLimitResult = await CheckIpRateLimitAsync(clientIp, path);
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

            // Check Virtual Key rate limits (if authenticated and enabled)
            if (_options.VirtualKey.EnforceRateLimits && context.Items.ContainsKey("VirtualKeyEntity"))
            {
                var virtualKey = context.Items["VirtualKeyEntity"] as VirtualKey;
                if (virtualKey != null && (virtualKey.RateLimitRpm.HasValue || virtualKey.RateLimitRpd.HasValue))
                {
                    var vkeyRateLimitResult = await CheckVirtualKeyRateLimitAsync(context, virtualKey.Id.ToString(), path);
                    if (!vkeyRateLimitResult.IsAllowed)
                    {
                        return new SecurityCheckResult
                        {
                            IsAllowed = false,
                            Reason = "Virtual Key rate limit exceeded",
                            StatusCode = 429,
                            Headers = new Dictionary<string, string>
                            {
                                ["X-RateLimit-Limit"] = vkeyRateLimitResult.Limit?.ToString() ?? "0",
                                ["X-RateLimit-Remaining"] = vkeyRateLimitResult.Remaining?.ToString() ?? "0",
                                ["X-RateLimit-Reset"] = vkeyRateLimitResult.ResetsAt?.ToUnixTimeSeconds().ToString() ?? ""
                            }
                        };
                    }
                }
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        /// <inheritdoc/>
        public async Task RecordFailedAuthAsync(string ipAddress, string attemptedKey)
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
                    var data = JsonSerializer.Deserialize<FailedAuthData>(cachedValue);
                    attempts = data?.Attempts ?? 0;
                }
            }
            else
            {
                attempts = _memoryCache.Get<int>(key);
            }

            attempts++;

            // Log the attempt
            _logger.LogWarning("Failed authentication attempt {Attempts}/{MaxAttempts} for IP {IpAddress} with key {Key}", 
                attempts, _options.FailedAuth.MaxAttempts, ipAddress, 
                attemptedKey.Length > 10 ? attemptedKey.Substring(0, 10) + "..." : attemptedKey);

            // Check if we should ban the IP
            if (attempts >= _options.FailedAuth.MaxAttempts)
            {
                var banInfo = new BannedIpInfo
                {
                    BannedUntil = DateTime.UtcNow.AddMinutes(_options.FailedAuth.BanDurationMinutes),
                    FailedAttempts = attempts,
                    Source = SERVICE_NAME,
                    Reason = "Exceeded max failed Virtual Key authentication attempts",
                    LastAttemptedKey = attemptedKey.Length > 10 ? attemptedKey.Substring(0, 10) + "..." : attemptedKey
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

                _logger.LogWarning("IP {IpAddress} has been banned after {Attempts} failed Virtual Key authentication attempts", 
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
                    LastAttempt = DateTime.UtcNow,
                    LastAttemptedKey = attemptedKey.Length > 10 ? attemptedKey.Substring(0, 10) + "..." : attemptedKey
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
            }
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

            _logger.LogDebug("Cleared failed authentication attempts for IP {IpAddress} after successful auth", ipAddress);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task<RateLimitCheckResult> CheckVirtualKeyRateLimitAsync(HttpContext context, string virtualKeyId, string endpoint)
        {
            // Get the Virtual Key entity from context to check its limits
            if (!context.Items.ContainsKey("VirtualKeyEntity"))
            {
                return new RateLimitCheckResult { IsAllowed = true };
            }

            var virtualKey = context.Items["VirtualKeyEntity"] as VirtualKey;
            if (virtualKey == null)
            {
                return new RateLimitCheckResult { IsAllowed = true };
            }

            var now = DateTime.UtcNow;
            var result = new RateLimitCheckResult { IsAllowed = true };

            // Check RPM (Requests Per Minute) limit
            if (virtualKey.RateLimitRpm.HasValue && virtualKey.RateLimitRpm.Value > 0)
            {
                var rpmKey = $"{VKEY_RATE_LIMIT_PREFIX}rpm:{virtualKeyId}";
                var rpmCount = await GetRateLimitCountAsync(rpmKey, 60); // 60 seconds window
                
                if (rpmCount >= virtualKey.RateLimitRpm.Value)
                {
                    _logger.LogWarning("Virtual Key {KeyId} exceeded RPM limit: {Count}/{Limit}", 
                        virtualKeyId, rpmCount, virtualKey.RateLimitRpm.Value);
                    
                    result.IsAllowed = false;
                    result.Limit = virtualKey.RateLimitRpm.Value;
                    result.Remaining = 0;
                    result.ResetsAt = now.AddSeconds(60);
                    return result;
                }

                // Increment counter
                await IncrementRateLimitCountAsync(rpmKey, 60);
                result.Limit = virtualKey.RateLimitRpm.Value;
                result.Remaining = virtualKey.RateLimitRpm.Value - (rpmCount + 1);
            }

            // Check RPD (Requests Per Day) limit
            if (virtualKey.RateLimitRpd.HasValue && virtualKey.RateLimitRpd.Value > 0)
            {
                var rpdKey = $"{VKEY_RATE_LIMIT_PREFIX}rpd:{virtualKeyId}";
                var rpdCount = await GetRateLimitCountAsync(rpdKey, 86400); // 24 hours in seconds
                
                if (rpdCount >= virtualKey.RateLimitRpd.Value)
                {
                    _logger.LogWarning("Virtual Key {KeyId} exceeded RPD limit: {Count}/{Limit}", 
                        virtualKeyId, rpdCount, virtualKey.RateLimitRpd.Value);
                    
                    result.IsAllowed = false;
                    result.Limit = virtualKey.RateLimitRpd.Value;
                    result.Remaining = 0;
                    result.ResetsAt = now.Date.AddDays(1); // Next day
                    return result;
                }

                // Increment counter
                await IncrementRateLimitCountAsync(rpdKey, 86400);
                
                // If we have RPM limit, that takes precedence for response headers
                if (!virtualKey.RateLimitRpm.HasValue)
                {
                    result.Limit = virtualKey.RateLimitRpd.Value;
                    result.Remaining = virtualKey.RateLimitRpd.Value - (rpdCount + 1);
                    result.ResetsAt = now.Date.AddDays(1);
                }
            }

            return result;
        }

        private async Task<int> GetRateLimitCountAsync(string key, int windowSeconds)
        {
            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cachedValue))
                {
                    if (int.TryParse(cachedValue, out var count))
                        return count;
                    
                    // Try to deserialize as complex object for backward compatibility
                    try
                    {
                        var data = JsonSerializer.Deserialize<RateLimitData>(cachedValue);
                        return data?.Count ?? 0;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
            else
            {
                return _memoryCache.Get<int>(key);
            }
            
            return 0;
        }

        private async Task IncrementRateLimitCountAsync(string key, int windowSeconds)
        {
            var currentCount = await GetRateLimitCountAsync(key, windowSeconds);
            currentCount++;

            if (_options.UseDistributedTracking && _distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    currentCount.ToString(),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(windowSeconds)
                    });
            }
            else
            {
                _memoryCache.Set(key, currentCount, TimeSpan.FromSeconds(windowSeconds));
            }
        }

        private async Task<SecurityCheckResult> CheckIpRateLimitAsync(string ipAddress, string path = "")
        {
            // Check discovery-specific rate limiting first
            if (_options.RateLimiting.Discovery.Enabled && IsDiscoveryPath(path))
            {
                var discoveryResult = await CheckDiscoveryRateLimitAsync(ipAddress, path);
                if (!discoveryResult.IsAllowed)
                {
                    return discoveryResult;
                }
            }

            // Check general IP rate limiting
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
                _logger.LogWarning("IP rate limit exceeded for {IpAddress}: {Count} requests in {Window} seconds",
                    ipAddress, requestCount, _options.RateLimiting.WindowSeconds);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = $"Rate limit exceeded for path {path}",
                    StatusCode = 429,
                    Headers = new Dictionary<string, string>
                    {
                        ["Retry-After"] = _options.RateLimiting.WindowSeconds.ToString(),
                        ["X-RateLimit-Limit"] = _options.RateLimiting.MaxRequests.ToString(),
                        ["X-RateLimit-Scope"] = "general"
                    }
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

        /// <summary>
        /// Checks if the path is a discovery-related endpoint
        /// </summary>
        private bool IsDiscoveryPath(string path)
        {
            return _options.RateLimiting.Discovery.DiscoveryPaths
                .Any(discoveryPath => path.Contains(discoveryPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks discovery-specific rate limits
        /// </summary>
        private async Task<SecurityCheckResult> CheckDiscoveryRateLimitAsync(string ipAddress, string path)
        {
            var discoveryKey = $"{RATE_LIMIT_PREFIX}discovery:{ipAddress}";
            var now = DateTime.UtcNow;

            // Get current discovery request count
            var discoveryCount = await GetRateLimitCountAsync(discoveryKey, _options.RateLimiting.Discovery.WindowSeconds);
            discoveryCount++;

            if (discoveryCount > _options.RateLimiting.Discovery.MaxRequests)
            {
                _logger.LogWarning("Discovery rate limit exceeded for {IpAddress}: {Count} requests in {Window} seconds for path {Path}",
                    ipAddress, discoveryCount, _options.RateLimiting.Discovery.WindowSeconds, path);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = $"Discovery rate limit exceeded for path {path}",
                    StatusCode = 429,
                    Headers = new Dictionary<string, string>
                    {
                        ["Retry-After"] = _options.RateLimiting.Discovery.WindowSeconds.ToString(),
                        ["X-RateLimit-Limit"] = _options.RateLimiting.Discovery.MaxRequests.ToString(),
                        ["X-RateLimit-Scope"] = "discovery",
                        ["X-RateLimit-Remaining"] = Math.Max(0, _options.RateLimiting.Discovery.MaxRequests - discoveryCount).ToString()
                    }
                };
            }

            // Check per-model capability rate limiting for capability endpoints
            if (path.Contains("/capabilities/", StringComparison.OrdinalIgnoreCase))
            {
                var modelMatch = ExtractModelFromPath(path);
                if (!string.IsNullOrEmpty(modelMatch))
                {
                    var capabilityResult = await CheckModelCapabilityRateLimitAsync(ipAddress, modelMatch);
                    if (!capabilityResult.IsAllowed)
                    {
                        return capabilityResult;
                    }
                }
            }

            // Increment discovery counter
            await IncrementRateLimitCountAsync(discoveryKey, _options.RateLimiting.Discovery.WindowSeconds);

            return new SecurityCheckResult { IsAllowed = true };
        }

        /// <summary>
        /// Checks per-model capability rate limits
        /// </summary>
        private async Task<SecurityCheckResult> CheckModelCapabilityRateLimitAsync(string ipAddress, string modelName)
        {
            var capabilityKey = $"{RATE_LIMIT_PREFIX}capability:{ipAddress}:{modelName}";
            var now = DateTime.UtcNow;

            var capabilityCount = await GetRateLimitCountAsync(capabilityKey, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);
            capabilityCount++;

            if (capabilityCount > _options.RateLimiting.Discovery.MaxCapabilityChecksPerModel)
            {
                _logger.LogWarning("Model capability rate limit exceeded for {IpAddress} and model {Model}: {Count} requests in {Window} seconds",
                    ipAddress, modelName, capabilityCount, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);

                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = $"Capability check rate limit exceeded for model {modelName}",
                    StatusCode = 429,
                    Headers = new Dictionary<string, string>
                    {
                        ["Retry-After"] = _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds.ToString(),
                        ["X-RateLimit-Limit"] = _options.RateLimiting.Discovery.MaxCapabilityChecksPerModel.ToString(),
                        ["X-RateLimit-Scope"] = "model-capability",
                        ["X-RateLimit-Model"] = modelName
                    }
                };
            }

            // Increment capability counter
            await IncrementRateLimitCountAsync(capabilityKey, _options.RateLimiting.Discovery.CapabilityCheckWindowSeconds);

            return new SecurityCheckResult { IsAllowed = true };
        }

        /// <summary>
        /// Extracts model name from capability path
        /// </summary>
        private string ExtractModelFromPath(string path)
        {
            try
            {
                // Match patterns like /v1/discovery/models/{model}/capabilities/{capability}
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < segments.Length - 2; i++)
                {
                    if (segments[i].Equals("models", StringComparison.OrdinalIgnoreCase) && 
                        i + 2 < segments.Length && 
                        segments[i + 2].Equals("capabilities", StringComparison.OrdinalIgnoreCase))
                    {
                        return segments[i + 1];
                    }
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        // Data structures for Redis storage (compatible with WebUI/Admin)
        private class FailedAuthData
        {
            public int Attempts { get; set; }
            public string Source { get; set; } = "";
            public DateTime LastAttempt { get; set; }
            public string LastAttemptedKey { get; set; } = "";
        }

        private class BannedIpInfo
        {
            public DateTime BannedUntil { get; set; }
            public int FailedAttempts { get; set; }
            public string Source { get; set; } = "";
            public string Reason { get; set; } = "";
            public string LastAttemptedKey { get; set; } = "";
        }

        private class RateLimitData
        {
            public int Count { get; set; }
            public string Source { get; set; } = "";
            public DateTime WindowStart { get; set; }
        }
    }

    // Extension method for DateTime to Unix timestamp
    internal static class DateTimeExtensions
    {
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }
    }
}