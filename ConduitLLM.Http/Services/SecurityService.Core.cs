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
using ConduitLLM.Security.Interfaces;

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
    public partial class SecurityService : ISecurityService
    {
        private readonly SecurityOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecurityService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISecurityEventMonitoringService? _securityEventMonitoring;

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
            IServiceProvider serviceProvider)
        {
            _options = options.Value;
            _configuration = configuration;
            _logger = logger;
            _memoryCache = memoryCache;
            _distributedCache = serviceProvider.GetService<IDistributedCache>();
            _serviceProvider = serviceProvider;
            _securityEventMonitoring = serviceProvider.GetService<ISecurityEventMonitoringService>();
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
                
                // Record in security event monitoring
                _securityEventMonitoring?.RecordAuthenticationFailure(clientIp, attemptedKey, path);
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
                
                // Record successful authentication
                var virtualKey = context.Items["VirtualKey"] as string ?? "";
                _securityEventMonitoring?.RecordAuthenticationSuccess(clientIp, virtualKey, path);
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
    }
}