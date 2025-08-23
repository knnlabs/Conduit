using ConduitLLM.Admin.Options;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for configuring security options
    /// </summary>
    public static class SecurityOptionsExtensions
    {
        /// <summary>
        /// Configures security options from environment variables
        /// </summary>
        public static IServiceCollection ConfigureAdminSecurityOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SecurityOptions>(options =>
            {
                // IP Filtering
                options.IpFiltering.Enabled = configuration.GetValue<bool>("CONDUIT_ADMIN_IP_FILTERING_ENABLED", false);
                options.IpFiltering.Mode = configuration["CONDUIT_ADMIN_IP_FILTER_MODE"] ?? "permissive";
                options.IpFiltering.AllowPrivateIps = configuration.GetValue<bool>("CONDUIT_ADMIN_IP_FILTER_ALLOW_PRIVATE", true);
                
                // Parse whitelist and blacklist from comma-separated values
                var whitelist = configuration["CONDUIT_ADMIN_IP_FILTER_WHITELIST"];
                if (!string.IsNullOrWhiteSpace(whitelist))
                {
                    options.IpFiltering.Whitelist = whitelist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }
                
                var blacklist = configuration["CONDUIT_ADMIN_IP_FILTER_BLACKLIST"];
                if (!string.IsNullOrWhiteSpace(blacklist))
                {
                    options.IpFiltering.Blacklist = blacklist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }

                // Rate Limiting
                options.RateLimiting.Enabled = configuration.GetValue<bool>("CONDUIT_ADMIN_RATE_LIMITING_ENABLED", false);
                options.RateLimiting.MaxRequests = configuration.GetValue<int>("CONDUIT_ADMIN_RATE_LIMIT_MAX_REQUESTS", 100);
                options.RateLimiting.WindowSeconds = configuration.GetValue<int>("CONDUIT_ADMIN_RATE_LIMIT_WINDOW_SECONDS", 60);
                
                var rateLimitExcluded = configuration["CONDUIT_ADMIN_RATE_LIMIT_EXCLUDED_PATHS"];
                if (!string.IsNullOrWhiteSpace(rateLimitExcluded))
                {
                    options.RateLimiting.ExcludedPaths = rateLimitExcluded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }

                // Failed Authentication Protection
                options.FailedAuth.Enabled = configuration.GetValue<bool>("CONDUIT_ADMIN_IP_BANNING_ENABLED", true);
                options.FailedAuth.MaxAttempts = configuration.GetValue<int>("CONDUIT_ADMIN_MAX_FAILED_AUTH_ATTEMPTS", 5);
                options.FailedAuth.BanDurationMinutes = configuration.GetValue<int>("CONDUIT_ADMIN_AUTH_BAN_DURATION_MINUTES", 30);

                // Distributed Tracking (shared with WebUI)
                options.UseDistributedTracking = configuration.GetValue<bool>("CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING", true);

                // Security Headers
                var headers = options.Headers;
                
                // X-Content-Type-Options
                headers.XContentTypeOptions = configuration.GetValue<bool>("CONDUIT_ADMIN_SECURITY_HEADERS_X_CONTENT_TYPE_OPTIONS_ENABLED", true);
                
                // X-XSS-Protection
                headers.XXssProtection = configuration.GetValue<bool>("CONDUIT_ADMIN_SECURITY_HEADERS_X_XSS_PROTECTION_ENABLED", true);
                
                // HSTS
                headers.Hsts.Enabled = configuration.GetValue<bool>("CONDUIT_ADMIN_SECURITY_HEADERS_HSTS_ENABLED", true);
                headers.Hsts.MaxAge = configuration.GetValue<int>("CONDUIT_ADMIN_SECURITY_HEADERS_HSTS_MAX_AGE", 31536000);

                // API Authentication
                options.ApiAuth.ApiKeyHeader = configuration["CONDUIT_ADMIN_API_KEY_HEADER"] ?? "X-API-Key";
                
                var altHeaders = configuration["CONDUIT_ADMIN_API_KEY_ALT_HEADERS"];
                if (!string.IsNullOrWhiteSpace(altHeaders))
                {
                    options.ApiAuth.AlternativeHeaders = altHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToList();
                }
            });

            return services;
        }
    }
}