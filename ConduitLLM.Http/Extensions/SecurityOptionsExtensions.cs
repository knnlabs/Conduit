using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.Http.Options;

namespace ConduitLLM.Http.Extensions
{
    /// <summary>
    /// Extension methods for configuring Core API security options
    /// </summary>
    public static class SecurityOptionsExtensions
    {
        /// <summary>
        /// Configures Core API security options from configuration
        /// </summary>
        public static IServiceCollection ConfigureCoreApiSecurityOptions(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            services.Configure<SecurityOptions>(options =>
            {
                // IP Filtering
                options.IpFiltering.Enabled = configuration.GetValue<bool?>("CONDUIT_CORE_IP_FILTERING_ENABLED") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:IpFiltering:Enabled", true);
                
                options.IpFiltering.Mode = configuration["CONDUIT_CORE_IP_FILTER_MODE"] 
                    ?? configuration["CoreApi:Security:IpFiltering:Mode"] 
                    ?? "permissive";
                
                options.IpFiltering.AllowPrivateIps = configuration.GetValue<bool?>("CONDUIT_CORE_IP_FILTER_ALLOW_PRIVATE") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:IpFiltering:AllowPrivateIps", true);

                // Parse whitelist
                var whitelist = configuration["CONDUIT_CORE_IP_FILTER_WHITELIST"] 
                    ?? configuration["CoreApi:Security:IpFiltering:Whitelist"];
                if (!string.IsNullOrEmpty(whitelist))
                {
                    options.IpFiltering.Whitelist = whitelist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(ip => ip.Trim())
                        .ToList();
                }

                // Parse blacklist
                var blacklist = configuration["CONDUIT_CORE_IP_FILTER_BLACKLIST"] 
                    ?? configuration["CoreApi:Security:IpFiltering:Blacklist"];
                if (!string.IsNullOrEmpty(blacklist))
                {
                    options.IpFiltering.Blacklist = blacklist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(ip => ip.Trim())
                        .ToList();
                }

                // Rate Limiting (IP-based)
                options.RateLimiting.Enabled = configuration.GetValue<bool?>("CONDUIT_CORE_RATE_LIMITING_ENABLED") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:RateLimiting:Enabled", true);
                
                options.RateLimiting.MaxRequests = configuration.GetValue<int?>("CONDUIT_CORE_RATE_LIMIT_MAX_REQUESTS") 
                    ?? configuration.GetValue<int>("CoreApi:Security:RateLimiting:MaxRequests", 1000);
                
                options.RateLimiting.WindowSeconds = configuration.GetValue<int?>("CONDUIT_CORE_RATE_LIMIT_WINDOW_SECONDS") 
                    ?? configuration.GetValue<int>("CoreApi:Security:RateLimiting:WindowSeconds", 60);

                // Parse excluded paths for rate limiting
                var rateLimitExcluded = configuration["CONDUIT_CORE_RATE_LIMIT_EXCLUDED_PATHS"] 
                    ?? configuration["CoreApi:Security:RateLimiting:ExcludedPaths"];
                if (!string.IsNullOrEmpty(rateLimitExcluded))
                {
                    options.RateLimiting.ExcludedPaths = rateLimitExcluded.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(path => path.Trim())
                        .ToList();
                }

                // Failed Authentication Protection
                options.FailedAuth.MaxAttempts = configuration.GetValue<int?>("CONDUIT_CORE_MAX_FAILED_AUTH_ATTEMPTS") 
                    ?? configuration.GetValue<int>("CoreApi:Security:FailedAuth:MaxAttempts", 10);
                
                options.FailedAuth.BanDurationMinutes = configuration.GetValue<int?>("CONDUIT_CORE_AUTH_BAN_DURATION_MINUTES") 
                    ?? configuration.GetValue<int>("CoreApi:Security:FailedAuth:BanDurationMinutes", 30);
                
                options.FailedAuth.TrackAcrossKeys = configuration.GetValue<bool?>("CONDUIT_CORE_TRACK_FAILED_AUTH_ACROSS_KEYS") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:FailedAuth:TrackAcrossKeys", true);

                // Security Headers
                options.Headers.XContentTypeOptions = configuration.GetValue<bool?>("CONDUIT_CORE_SECURITY_HEADERS_CONTENT_TYPE") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:Headers:XContentTypeOptions", true);
                
                options.Headers.XXssProtection = configuration.GetValue<bool?>("CONDUIT_CORE_SECURITY_HEADERS_XSS") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:Headers:XXssProtection", false);
                
                options.Headers.Hsts.Enabled = configuration.GetValue<bool?>("CONDUIT_CORE_SECURITY_HEADERS_HSTS_ENABLED") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:Headers:Hsts:Enabled", true);
                
                options.Headers.Hsts.MaxAge = configuration.GetValue<int?>("CONDUIT_CORE_SECURITY_HEADERS_HSTS_MAX_AGE") 
                    ?? configuration.GetValue<int>("CoreApi:Security:Headers:Hsts:MaxAge", 31536000);

                // Distributed Tracking
                options.UseDistributedTracking = configuration.GetValue<bool?>("CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING") 
                    ?? configuration.GetValue<bool>("Security:UseDistributedTracking", true);

                // Virtual Key Options
                options.VirtualKey.EnforceRateLimits = configuration.GetValue<bool?>("CONDUIT_CORE_ENFORCE_VKEY_RATE_LIMITS") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:VirtualKey:EnforceRateLimits", true);
                
                options.VirtualKey.EnforceBudgetLimits = configuration.GetValue<bool?>("CONDUIT_CORE_ENFORCE_VKEY_BUDGETS") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:VirtualKey:EnforceBudgetLimits", true);
                
                options.VirtualKey.EnforceModelRestrictions = configuration.GetValue<bool?>("CONDUIT_CORE_ENFORCE_VKEY_MODELS") 
                    ?? configuration.GetValue<bool>("CoreApi:Security:VirtualKey:EnforceModelRestrictions", true);
                
                options.VirtualKey.ValidationCacheSeconds = configuration.GetValue<int?>("CONDUIT_CORE_VKEY_CACHE_SECONDS") 
                    ?? configuration.GetValue<int>("CoreApi:Security:VirtualKey:ValidationCacheSeconds", 60);
            });

            return services;
        }
    }
}