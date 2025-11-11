namespace ConduitLLM.Admin.Options
{
    /// <summary>
    /// Security configuration options for the Admin API
    /// </summary>
    public class SecurityOptions
    {
        /// <summary>
        /// IP filtering configuration
        /// </summary>
        public IpFilteringOptions IpFiltering { get; set; } = new();

        /// <summary>
        /// Rate limiting configuration
        /// </summary>
        public RateLimitingOptions RateLimiting { get; set; } = new();

        /// <summary>
        /// Failed authentication protection configuration
        /// </summary>
        public FailedAuthOptions FailedAuth { get; set; } = new();

        /// <summary>
        /// Security headers configuration
        /// </summary>
        public SecurityHeadersOptions Headers { get; set; } = new();

        /// <summary>
        /// Whether to use distributed (Redis) tracking for security features
        /// </summary>
        public bool UseDistributedTracking { get; set; } = true;

        /// <summary>
        /// API authentication configuration
        /// </summary>
        public ApiAuthOptions ApiAuth { get; set; } = new();
    }

    /// <summary>
    /// IP filtering options
    /// </summary>
    public class IpFilteringOptions
    {
        /// <summary>
        /// Whether IP filtering is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Filter mode: "permissive" (blacklist) or "restrictive" (whitelist)
        /// </summary>
        public string Mode { get; set; } = "permissive";

        /// <summary>
        /// Whether to allow private/intranet IPs
        /// </summary>
        public bool AllowPrivateIps { get; set; } = true;

        /// <summary>
        /// List of whitelisted IPs or CIDR ranges
        /// </summary>
        public List<string> Whitelist { get; set; } = new();

        /// <summary>
        /// List of blacklisted IPs or CIDR ranges
        /// </summary>
        public List<string> Blacklist { get; set; } = new();

        /// <summary>
        /// Paths excluded from IP filtering
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new()
        {
            "/health",
            "/swagger"
        };
    }

    /// <summary>
    /// Rate limiting options
    /// </summary>
    public class RateLimitingOptions
    {
        /// <summary>
        /// Whether rate limiting is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Maximum number of requests allowed per window
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// Time window in seconds
        /// </summary>
        public int WindowSeconds { get; set; } = 60;

        /// <summary>
        /// Paths excluded from rate limiting
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new()
        {
            "/health",
            "/swagger"
        };
    }

    /// <summary>
    /// Failed authentication protection options
    /// </summary>
    public class FailedAuthOptions
    {
        /// <summary>
        /// Whether IP banning is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of failed authentication attempts before banning
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// Duration in minutes for which an IP is banned
        /// </summary>
        public int BanDurationMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Security headers options
    /// </summary>
    public class SecurityHeadersOptions
    {
        /// <summary>
        /// X-Content-Type-Options header
        /// </summary>
        public bool XContentTypeOptions { get; set; } = true;

        /// <summary>
        /// X-XSS-Protection header
        /// </summary>
        public bool XXssProtection { get; set; } = true;

        /// <summary>
        /// Strict-Transport-Security options
        /// </summary>
        public HstsOptions Hsts { get; set; } = new();

        /// <summary>
        /// Custom headers to add
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
    }

    /// <summary>
    /// HSTS (HTTP Strict Transport Security) options
    /// </summary>
    public class HstsOptions
    {
        /// <summary>
        /// Whether HSTS is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Max age in seconds
        /// </summary>
        public int MaxAge { get; set; } = 31536000; // 1 year
    }

    /// <summary>
    /// API authentication options
    /// </summary>
    public class ApiAuthOptions
    {
        /// <summary>
        /// Header name for API key
        /// </summary>
        public string ApiKeyHeader { get; set; } = "X-API-Key";

        /// <summary>
        /// Alternative header names for backward compatibility
        /// </summary>
        public List<string> AlternativeHeaders { get; set; } = new()
        {
            "X-Master-Key"
        };
    }
}