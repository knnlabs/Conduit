namespace ConduitLLM.Http.Options
{
    /// <summary>
    /// Security configuration options for the Core API
    /// </summary>
    public class SecurityOptions
    {
        /// <summary>
        /// IP filtering options
        /// </summary>
        public IpFilteringOptions IpFiltering { get; set; } = new();

        /// <summary>
        /// Rate limiting options for IP-based limits (not Virtual Key limits)
        /// </summary>
        public RateLimitingOptions RateLimiting { get; set; } = new();

        /// <summary>
        /// Failed authentication protection options
        /// </summary>
        public FailedAuthOptions FailedAuth { get; set; } = new();

        /// <summary>
        /// Security headers options
        /// </summary>
        public SecurityHeadersOptions Headers { get; set; } = new();

        /// <summary>
        /// Whether to use distributed tracking via Redis
        /// </summary>
        public bool UseDistributedTracking { get; set; } = true;

        /// <summary>
        /// Virtual Key specific options
        /// </summary>
        public VirtualKeyOptions VirtualKey { get; set; } = new();
    }

    /// <summary>
    /// IP filtering configuration
    /// </summary>
    public class IpFilteringOptions
    {
        /// <summary>
        /// Whether IP filtering is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Filter mode: "permissive" (blacklist) or "restrictive" (whitelist)
        /// </summary>
        public string Mode { get; set; } = "permissive";

        /// <summary>
        /// Whether to allow private/intranet IPs
        /// </summary>
        public bool AllowPrivateIps { get; set; } = true;

        /// <summary>
        /// IP addresses or CIDR ranges to whitelist
        /// </summary>
        public List<string> Whitelist { get; set; } = new();

        /// <summary>
        /// IP addresses or CIDR ranges to blacklist
        /// </summary>
        public List<string> Blacklist { get; set; } = new();

        /// <summary>
        /// Paths to exclude from IP filtering
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new() { "/health", "/metrics" };
    }

    /// <summary>
    /// Rate limiting configuration for IP-based limits
    /// </summary>
    public class RateLimitingOptions
    {
        /// <summary>
        /// Whether IP-based rate limiting is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum requests per IP per window
        /// </summary>
        public int MaxRequests { get; set; } = 1000;

        /// <summary>
        /// Time window in seconds
        /// </summary>
        public int WindowSeconds { get; set; } = 60;

        /// <summary>
        /// Discovery-specific rate limiting configuration
        /// </summary>
        public DiscoveryRateLimitOptions Discovery { get; set; } = new();

        /// <summary>
        /// Paths to exclude from rate limiting
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new() { "/health", "/metrics", "/swagger" };
    }

    /// <summary>
    /// Discovery API specific rate limiting configuration
    /// </summary>
    public class DiscoveryRateLimitOptions
    {
        /// <summary>
        /// Whether discovery-specific rate limiting is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum discovery requests per IP per window
        /// </summary>
        public int MaxRequests { get; set; } = 500; // Increased from 100 with bulk API

        /// <summary>
        /// Time window in seconds for discovery requests
        /// </summary>
        public int WindowSeconds { get; set; } = 300; // 5 minutes

        /// <summary>
        /// Paths that count towards discovery rate limits
        /// </summary>
        public List<string> DiscoveryPaths { get; set; } = new() 
        { 
            "/v1/discovery/", 
            "/v1/models/",
            "/capabilities/"
        };

        /// <summary>
        /// Maximum capability check requests per model per IP per window
        /// </summary>
        public int MaxCapabilityChecksPerModel { get; set; } = 20; // Increased from 5

        /// <summary>
        /// Time window for per-model capability checks in seconds
        /// </summary>
        public int CapabilityCheckWindowSeconds { get; set; } = 600; // 10 minutes
    }

    /// <summary>
    /// Failed authentication protection configuration
    /// </summary>
    public class FailedAuthOptions
    {
        /// <summary>
        /// Maximum failed authentication attempts per IP before banning
        /// </summary>
        public int MaxAttempts { get; set; } = 10;

        /// <summary>
        /// Duration to ban an IP in minutes
        /// </summary>
        public int BanDurationMinutes { get; set; } = 30;

        /// <summary>
        /// Whether to track failed attempts across all Virtual Keys
        /// </summary>
        public bool TrackAcrossKeys { get; set; } = true;
    }

    /// <summary>
    /// Security headers configuration
    /// </summary>
    public class SecurityHeadersOptions
    {
        /// <summary>
        /// Whether to add X-Content-Type-Options header
        /// </summary>
        public bool XContentTypeOptions { get; set; } = true;

        /// <summary>
        /// Whether to add X-XSS-Protection header
        /// </summary>
        public bool XXssProtection { get; set; } = false; // Not needed for API

        /// <summary>
        /// HSTS configuration
        /// </summary>
        public HstsOptions Hsts { get; set; } = new();

        /// <summary>
        /// Custom headers to add
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
    }

    /// <summary>
    /// HSTS configuration
    /// </summary>
    public class HstsOptions
    {
        /// <summary>
        /// Whether HSTS is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// HSTS max age in seconds
        /// </summary>
        public int MaxAge { get; set; } = 31536000; // 1 year
    }

    /// <summary>
    /// Virtual Key specific options
    /// </summary>
    public class VirtualKeyOptions
    {
        /// <summary>
        /// Whether to enforce Virtual Key rate limits from database
        /// </summary>
        public bool EnforceRateLimits { get; set; } = true;

        /// <summary>
        /// Whether to enforce Virtual Key budget limits
        /// </summary>
        public bool EnforceBudgetLimits { get; set; } = true;

        /// <summary>
        /// Whether to enforce model access restrictions
        /// </summary>
        public bool EnforceModelRestrictions { get; set; } = true;

        /// <summary>
        /// Cache duration for Virtual Key validation in seconds
        /// </summary>
        public int ValidationCacheSeconds { get; set; } = 60;

        /// <summary>
        /// Headers to check for Virtual Key (in order of preference)
        /// </summary>
        public List<string> KeyHeaders { get; set; } = new() 
        { 
            "Authorization", 
            "api-key", 
            "X-API-Key", 
            "X-Virtual-Key" 
        };
    }
}