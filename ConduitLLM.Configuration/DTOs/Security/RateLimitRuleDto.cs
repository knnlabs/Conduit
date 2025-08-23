namespace ConduitLLM.Configuration.DTOs.Security
{
    /// <summary>
    /// Represents a rate limiting rule
    /// </summary>
    public class RateLimitRuleDto
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the rule
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this rule does
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Type of rate limit
        /// </summary>
        public RateLimitType LimitType { get; set; }

        /// <summary>
        /// Scope of the rate limit
        /// </summary>
        public RateLimitScope Scope { get; set; }

        /// <summary>
        /// Maximum number of requests allowed
        /// </summary>
        public int MaxRequests { get; set; }

        /// <summary>
        /// Time window in seconds
        /// </summary>
        public int WindowSeconds { get; set; }

        /// <summary>
        /// Specific endpoint pattern (if applicable)
        /// </summary>
        public string? EndpointPattern { get; set; }

        /// <summary>
        /// Whether the rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Priority of the rule (higher = evaluated first)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Action to take when limit is exceeded
        /// </summary>
        public RateLimitAction Action { get; set; }

        /// <summary>
        /// Custom error message
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When the rule was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the rule was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Whether to apply to authenticated requests
        /// </summary>
        public bool ApplyToAuthenticated { get; set; } = true;

        /// <summary>
        /// Whether to apply to anonymous requests
        /// </summary>
        public bool ApplyToAnonymous { get; set; } = true;
    }

    /// <summary>
    /// Types of rate limits
    /// </summary>
    public enum RateLimitType
    {
        /// <summary>
        /// Fixed window rate limit
        /// </summary>
        FixedWindow,

        /// <summary>
        /// Sliding window rate limit
        /// </summary>
        SlidingWindow,

        /// <summary>
        /// Token bucket algorithm
        /// </summary>
        TokenBucket,

        /// <summary>
        /// Concurrent request limit
        /// </summary>
        Concurrent
    }

    /// <summary>
    /// Scope of rate limit application
    /// </summary>
    public enum RateLimitScope
    {
        /// <summary>
        /// Apply per IP address
        /// </summary>
        PerIp,

        /// <summary>
        /// Apply per virtual key
        /// </summary>
        PerVirtualKey,

        /// <summary>
        /// Apply per IP and virtual key combination
        /// </summary>
        PerIpAndKey,

        /// <summary>
        /// Apply globally
        /// </summary>
        Global,

        /// <summary>
        /// Apply per endpoint
        /// </summary>
        PerEndpoint
    }

    /// <summary>
    /// Action to take when rate limit is exceeded
    /// </summary>
    public enum RateLimitAction
    {
        /// <summary>
        /// Reject the request
        /// </summary>
        Reject,

        /// <summary>
        /// Throttle/delay the request
        /// </summary>
        Throttle,

        /// <summary>
        /// Log but allow the request
        /// </summary>
        LogOnly,

        /// <summary>
        /// Temporarily ban the source
        /// </summary>
        TempBan
    }
}