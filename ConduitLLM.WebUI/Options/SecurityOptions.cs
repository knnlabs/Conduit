using System.Collections.Generic;

namespace ConduitLLM.WebUI.Options
{
    /// <summary>
    /// Unified security configuration options
    /// </summary>
    public class SecurityOptions
    {
        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string SectionName = "Security";

        /// <summary>
        /// IP filtering configuration
        /// </summary>
        public IpFilteringOptions IpFiltering { get; set; } = new();

        /// <summary>
        /// Rate limiting configuration
        /// </summary>
        public RateLimitingOptions RateLimiting { get; set; } = new();

        /// <summary>
        /// Failed login protection configuration
        /// </summary>
        public FailedLoginOptions FailedLogin { get; set; } = new();

        /// <summary>
        /// Security headers configuration
        /// </summary>
        public SecurityHeadersOptions Headers { get; set; } = new();

        /// <summary>
        /// Whether to use distributed (Redis) tracking for security features
        /// </summary>
        public bool UseDistributedTracking { get; set; } = true;
    }

    /// <summary>
    /// IP filtering configuration options
    /// </summary>
    public class IpFilteringOptions
    {
        /// <summary>
        /// Whether IP filtering is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Filter mode: "permissive" (blacklist) or "restrictive" (whitelist)
        /// </summary>
        public string Mode { get; set; } = "permissive";

        /// <summary>
        /// Whether to automatically allow private/intranet IPs
        /// </summary>
        public bool AllowPrivateIps { get; set; } = true;

        /// <summary>
        /// List of whitelisted IP addresses or CIDR ranges
        /// </summary>
        public List<string> Whitelist { get; set; } = new();

        /// <summary>
        /// List of blacklisted IP addresses or CIDR ranges
        /// </summary>
        public List<string> Blacklist { get; set; } = new();

        /// <summary>
        /// Paths to exclude from IP filtering
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new()
        {
            "/health",
            "/health/ready",
            "/health/live",
            "/_blazor",
            "/css",
            "/js",
            "/images",
            "/favicon.svg"
        };
    }

    /// <summary>
    /// Rate limiting configuration options
    /// </summary>
    public class RateLimitingOptions
    {
        /// <summary>
        /// Whether rate limiting is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Maximum requests allowed per window
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// Time window in seconds
        /// </summary>
        public int WindowSeconds { get; set; } = 60;

        /// <summary>
        /// Paths to exclude from rate limiting
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new()
        {
            "/health",
            "/_blazor",
            "/css",
            "/js",
            "/images"
        };
    }

    /// <summary>
    /// Failed login protection configuration options
    /// </summary>
    public class FailedLoginOptions
    {
        /// <summary>
        /// Maximum failed login attempts before banning
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// IP ban duration in minutes
        /// </summary>
        public int BanDurationMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Security headers configuration options
    /// </summary>
    public class SecurityHeadersOptions
    {
        /// <summary>
        /// X-Frame-Options header configuration
        /// </summary>
        public HeaderOption XFrameOptions { get; set; } = new() { Enabled = true, Value = "DENY" };

        /// <summary>
        /// X-Content-Type-Options header configuration
        /// </summary>
        public bool XContentTypeOptions { get; set; } = true;

        /// <summary>
        /// X-XSS-Protection header configuration
        /// </summary>
        public bool XXssProtection { get; set; } = true;

        /// <summary>
        /// Referrer-Policy header configuration
        /// </summary>
        public HeaderOption ReferrerPolicy { get; set; } = new() { Enabled = true, Value = "strict-origin-when-cross-origin" };

        /// <summary>
        /// Content-Security-Policy header configuration
        /// </summary>
        public HeaderOption ContentSecurityPolicy { get; set; } = new()
        {
            Enabled = true,
            Value = "default-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com https://fonts.gstatic.com; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
                    "img-src 'self' data: https:; " +
                    "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
                    "connect-src 'self' wss: ws:; " +
                    "frame-ancestors 'none';"
        };

        /// <summary>
        /// Strict-Transport-Security header configuration
        /// </summary>
        public HstsOption Hsts { get; set; } = new() { Enabled = true, MaxAge = 31536000 };

        /// <summary>
        /// Permissions-Policy header configuration
        /// </summary>
        public HeaderOption PermissionsPolicy { get; set; } = new()
        {
            Enabled = true,
            Value = "camera=(), microphone=(), geolocation=(), payment=()"
        };
    }

    /// <summary>
    /// Generic header option
    /// </summary>
    public class HeaderOption
    {
        /// <summary>
        /// Whether the header is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The header value
        /// </summary>
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// HSTS header option
    /// </summary>
    public class HstsOption
    {
        /// <summary>
        /// Whether HSTS is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Max age in seconds
        /// </summary>
        public int MaxAge { get; set; }
    }
}