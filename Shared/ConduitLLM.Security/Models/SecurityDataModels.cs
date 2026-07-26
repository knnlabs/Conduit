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
}
