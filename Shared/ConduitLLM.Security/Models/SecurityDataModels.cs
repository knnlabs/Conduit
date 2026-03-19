namespace ConduitLLM.Security.Models
{
    /// <summary>
    /// Tracks failed authentication attempts for an IP address.
    /// Shared across Admin and Gateway for consistent Redis/cache storage.
    /// </summary>
    public class FailedAuthData
    {
        public int Attempts { get; set; }
        public string Source { get; set; } = "";
        public DateTime LastAttempt { get; set; }
        public string LastAttemptedKey { get; set; } = "";
    }

    /// <summary>
    /// Information about a banned IP address.
    /// Shared across Admin and Gateway for consistent Redis/cache storage.
    /// </summary>
    public class BannedIpInfo
    {
        public DateTime BannedUntil { get; set; }
        public int FailedAttempts { get; set; }
        public string Source { get; set; } = "";
        public string Reason { get; set; } = "";
        public string LastAttemptedKey { get; set; } = "";
    }

    /// <summary>
    /// Rate limit tracking data for an IP address.
    /// Shared across Admin and Gateway for consistent Redis/cache storage.
    /// </summary>
    public class RateLimitData
    {
        public int Count { get; set; }
        public string Source { get; set; } = "";
        public DateTime WindowStart { get; set; }
    }

    /// <summary>
    /// Result of a Virtual Key rate limit check (Gateway-specific).
    /// </summary>
    public class RateLimitCheckResult
    {
        public bool IsAllowed { get; set; }
        public int? Remaining { get; set; }
        public int? Limit { get; set; }
        public DateTime? ResetsAt { get; set; }
    }
}
