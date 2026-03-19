using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Models;
using ConduitLLM.Security.Options;
using ConduitLLM.Security.Services;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Gateway-specific security service interface.
    /// Extends the shared security service with Virtual Key rate limiting.
    /// </summary>
    public interface IGatewaySecurityService : ConduitLLM.Security.Interfaces.ISecurityService
    {
        /// <summary>
        /// Checks Virtual Key rate limits (RPM and RPD)
        /// </summary>
        Task<RateLimitCheckResult> CheckVirtualKeyRateLimitAsync(HttpContext context, string virtualKeyId, string endpoint);
    }

    /// <summary>
    /// Implementation of security service for Gateway API.
    /// Handles Virtual Key authentication, IP banning, rate limiting, IP filtering,
    /// discovery-specific rate limits, and security event monitoring.
    /// </summary>
    public partial class SecurityService : SecurityServiceBase, IGatewaySecurityService
    {
        private readonly GatewaySecurityOptions _options;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISecurityEventMonitoringService? _securityEventMonitoring;

        // Gateway-specific cache prefix
        private const string VkeyRateLimitPrefix = "vkey_rate:";

        /// <inheritdoc/>
        protected override string ServiceName => "core-api";

        /// <inheritdoc/>
        protected override SecurityOptionsBase Options => _options;

        /// <summary>
        /// Initializes a new instance of the Gateway SecurityService
        /// </summary>
        public SecurityService(
            IOptions<GatewaySecurityOptions> options,
            IConfiguration configuration,
            ILogger<SecurityService> logger,
            IMemoryCache memoryCache,
            IServiceProvider serviceProvider)
            : base(logger, memoryCache, serviceProvider.GetService<IDistributedCache>())
        {
            _options = options.Value;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _securityEventMonitoring = serviceProvider.GetService<ISecurityEventMonitoringService>();
        }

        /// <inheritdoc/>
        public override async Task<SecurityCheckResult> IsRequestAllowedAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var path = context.Request.Path.Value ?? "";

            // Skip security checks for excluded paths
            if (IsPathExcluded(path, new List<string> { "/health", "/metrics" }))
            {
                return SecurityCheckResult.Allowed();
            }

            // Check if authentication failed (set by VirtualKeyAuthenticationHandler)
            if (context.Items.ContainsKey("FailedAuth") && context.Items["FailedAuth"] is bool failedAuth && failedAuth)
            {
                var attemptedKey = context.Items["AttemptedKey"] as string ?? "unknown";
                await RecordFailedAuthAsync(clientIp, attemptedKey);
                _securityEventMonitoring?.RecordAuthenticationFailure(clientIp, attemptedKey, path);
            }

            // Check if IP is banned
            if (await IsIpBannedAsync(clientIp))
            {
                return SecurityCheckResult.Denied("IP is banned due to excessive failed authentication attempts");
            }

            // If authentication succeeded, clear failed attempts
            if (context.Items.ContainsKey("AuthSuccess") && context.Items["AuthSuccess"] is bool authSuccess && authSuccess)
            {
                await ClearFailedAuthAttemptsAsync(clientIp);
                var virtualKey = context.Items["VirtualKey"] as string ?? "";
                _securityEventMonitoring?.RecordAuthenticationSuccess(clientIp, virtualKey, path);
            }

            // Check IP-based rate limiting
            if (_options.RateLimiting.Enabled && !IsPathExcluded(path, _options.RateLimiting.ExcludedPaths))
            {
                var rateLimitResult = await CheckIpRateLimitWithDiscoveryAsync(clientIp, path);
                if (!rateLimitResult.IsAllowed)
                {
                    return rateLimitResult;
                }
            }

            // Check IP filtering
            if (_options.IpFiltering.Enabled && !IsPathExcluded(path, _options.IpFiltering.ExcludedPaths))
            {
                var ipFilterResult = await CheckIpFilterAsync(clientIp);
                if (!ipFilterResult.IsAllowed)
                {
                    return ipFilterResult;
                }
            }

            // Check Virtual Key rate limits
            if (_options.VirtualKey.EnforceRateLimits && context.Items.ContainsKey("VirtualKeyEntity"))
            {
                var virtualKey = context.Items["VirtualKeyEntity"] as VirtualKey;
                if (virtualKey != null && (virtualKey.RateLimitRpm.HasValue || virtualKey.RateLimitRpd.HasValue))
                {
                    var vkeyResult = await CheckVirtualKeyRateLimitAsync(context, virtualKey.Id.ToString(), path);
                    if (!vkeyResult.IsAllowed)
                    {
                        return new SecurityCheckResult
                        {
                            IsAllowed = false,
                            Reason = "Virtual Key rate limit exceeded",
                            StatusCode = 429,
                            Headers = new Dictionary<string, string>
                            {
                                ["X-RateLimit-Limit"] = vkeyResult.Limit?.ToString() ?? "0",
                                ["X-RateLimit-Remaining"] = vkeyResult.Remaining?.ToString() ?? "0",
                                ["X-RateLimit-Reset"] = vkeyResult.ResetsAt?.ToUnixTimeSeconds().ToString() ?? ""
                            }
                        };
                    }
                }
            }

            return SecurityCheckResult.Allowed();
        }

        /// <inheritdoc/>
        protected override void OnIpBanned(string ipAddress, BannedIpInfo banInfo, int attempts)
        {
            _securityEventMonitoring?.RecordIpBan(ipAddress, banInfo.Reason, attempts);
        }

        /// <inheritdoc/>
        protected override async Task<SecurityCheckResult> CheckDatabaseIpFilterAsync(string ipAddress)
        {
            using var scope = _serviceProvider.CreateScope();
            var ipFilterService = scope.ServiceProvider.GetRequiredService<Interfaces.IIpFilterService>();
            var isAllowedByDb = await ipFilterService.IsIpAllowedAsync(ipAddress);

            if (!isAllowedByDb)
            {
                Logger.LogWarning("IP {IpAddress} blocked by database IP filter", ipAddress);
                return SecurityCheckResult.Denied("IP address not allowed");
            }

            return SecurityCheckResult.Allowed();
        }
    }
}
