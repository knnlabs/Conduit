using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.WebUI.Options;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for configuring security options
    /// </summary>
    public static class SecurityOptionsExtensions
    {
        /// <summary>
        /// Configures security options from environment variables
        /// </summary>
        public static IServiceCollection ConfigureSecurityOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SecurityOptions>(options =>
            {
                // IP Filtering
                options.IpFiltering.Enabled = configuration.GetValue<bool>("CONDUIT_IP_FILTERING_ENABLED", false);
                options.IpFiltering.Mode = configuration["CONDUIT_IP_FILTER_MODE"] ?? "permissive";
                options.IpFiltering.AllowPrivateIps = configuration.GetValue<bool>("CONDUIT_IP_FILTER_ALLOW_PRIVATE", true);
                
                // Parse whitelist and blacklist from comma-separated values
                var whitelist = configuration["CONDUIT_IP_FILTER_WHITELIST"];
                if (!string.IsNullOrWhiteSpace(whitelist))
                {
                    options.IpFiltering.Whitelist = whitelist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }
                
                var blacklist = configuration["CONDUIT_IP_FILTER_BLACKLIST"];
                if (!string.IsNullOrWhiteSpace(blacklist))
                {
                    options.IpFiltering.Blacklist = blacklist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }

                // Rate Limiting
                options.RateLimiting.Enabled = configuration.GetValue<bool>("CONDUIT_RATE_LIMITING_ENABLED", false);
                options.RateLimiting.MaxRequests = configuration.GetValue<int>("CONDUIT_RATE_LIMIT_MAX_REQUESTS", 100);
                options.RateLimiting.WindowSeconds = configuration.GetValue<int>("CONDUIT_RATE_LIMIT_WINDOW_SECONDS", 60);
                
                var rateLimitExcluded = configuration["CONDUIT_RATE_LIMIT_EXCLUDED_PATHS"];
                if (!string.IsNullOrWhiteSpace(rateLimitExcluded))
                {
                    options.RateLimiting.ExcludedPaths = rateLimitExcluded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }

                // Failed Login Protection
                options.FailedLogin.MaxAttempts = configuration.GetValue<int>("CONDUIT_MAX_FAILED_ATTEMPTS", 5);
                options.FailedLogin.BanDurationMinutes = configuration.GetValue<int>("CONDUIT_IP_BAN_DURATION_MINUTES", 30);

                // Distributed Tracking
                options.UseDistributedTracking = configuration.GetValue<bool>("CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING", true);

                // Security Headers
                var headers = options.Headers;
                
                // X-Frame-Options
                headers.XFrameOptions.Enabled = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS_ENABLED", true);
                headers.XFrameOptions.Value = configuration["CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS"] ?? "DENY";
                
                // Other boolean headers
                headers.XContentTypeOptions = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_X_CONTENT_TYPE_OPTIONS_ENABLED", true);
                headers.XXssProtection = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_X_XSS_PROTECTION_ENABLED", true);
                
                // Referrer-Policy
                headers.ReferrerPolicy.Enabled = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_REFERRER_POLICY_ENABLED", true);
                headers.ReferrerPolicy.Value = configuration["CONDUIT_SECURITY_HEADERS_REFERRER_POLICY"] ?? "strict-origin-when-cross-origin";
                
                // Content-Security-Policy
                headers.ContentSecurityPolicy.Enabled = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_CSP_ENABLED", true);
                headers.ContentSecurityPolicy.Value = configuration["CONDUIT_SECURITY_HEADERS_CSP"] ?? headers.ContentSecurityPolicy.Value;
                
                // HSTS
                headers.Hsts.Enabled = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_HSTS_ENABLED", true);
                headers.Hsts.MaxAge = configuration.GetValue<int>("CONDUIT_SECURITY_HEADERS_HSTS_MAX_AGE", 31536000);
                
                // Permissions-Policy
                headers.PermissionsPolicy.Enabled = configuration.GetValue<bool>("CONDUIT_SECURITY_HEADERS_PERMISSIONS_POLICY_ENABLED", true);
                headers.PermissionsPolicy.Value = configuration["CONDUIT_SECURITY_HEADERS_PERMISSIONS_POLICY"] ?? headers.PermissionsPolicy.Value;
            });

            return services;
        }
    }
}