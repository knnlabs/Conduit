namespace ConduitLLM.Security.Options
{
    /// <summary>
    /// Security configuration options specific to the Admin API
    /// </summary>
    public class AdminSecurityOptions : SecurityOptionsBase
    {
        /// <summary>
        /// API authentication configuration
        /// </summary>
        public ApiAuthOptions ApiAuth { get; set; } = new();

        /// <summary>
        /// Initializes a new instance with Admin-specific defaults
        /// </summary>
        public AdminSecurityOptions()
        {
            // Admin API defaults - different from Gateway
            IpFiltering.Enabled = false; // Admin typically accessed from known IPs
            IpFiltering.ExcludedPaths = new List<string> { "/health", "/swagger" };

            RateLimiting.Enabled = false; // Admin operations less frequent
            RateLimiting.MaxRequests = 100;
            RateLimiting.ExcludedPaths = new List<string> { "/health", "/swagger" };

            Headers.XXssProtection = true; // Admin UI may render content
        }
    }

    /// <summary>
    /// Admin API rate limiting options
    /// </summary>
    public class AdminRateLimitingOptions : RateLimitingOptionsBase
    {
        /// <summary>
        /// Initializes with Admin-specific defaults
        /// </summary>
        public AdminRateLimitingOptions()
        {
            Enabled = false;
            MaxRequests = 100;
            WindowSeconds = 60;
            ExcludedPaths = new List<string> { "/health", "/swagger" };
        }
    }

    /// <summary>
    /// API authentication options for Admin API
    /// </summary>
    public class ApiAuthOptions
    {
        /// <summary>
        /// Header name for API key
        /// </summary>
        public string ApiKeyHeader { get; set; } = SecurityHeaderNames.ApiKey;

        /// <summary>
        /// Alternative header names for backward compatibility
        /// </summary>
        public List<string> AlternativeHeaders { get; set; } = new() { SecurityHeaderNames.MasterKey };
    }
}
