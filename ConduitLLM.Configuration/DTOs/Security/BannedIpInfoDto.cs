namespace ConduitLLM.Configuration.DTOs.Security
{
    /// <summary>
    /// Information about a banned IP address
    /// </summary>
    public class BannedIpInfoDto
    {
        /// <summary>
        /// The banned IP address
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Reason for the ban
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// When the ban was applied
        /// </summary>
        public DateTime BannedAt { get; set; }

        /// <summary>
        /// When the ban expires (null for permanent bans)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Number of failed attempts before ban
        /// </summary>
        public int FailedAttempts { get; set; }

        /// <summary>
        /// Service that initiated the ban
        /// </summary>
        public string BannedBy { get; set; } = string.Empty;

        /// <summary>
        /// Type of ban
        /// </summary>
        public BanType BanType { get; set; }

        /// <summary>
        /// Whether the ban is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Additional notes about the ban
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Security events that led to the ban
        /// </summary>
        public int RelatedSecurityEvents { get; set; }

        /// <summary>
        /// Whether this is a repeat offender
        /// </summary>
        public bool IsRepeatOffender { get; set; }

        /// <summary>
        /// Previous ban count for this IP
        /// </summary>
        public int PreviousBanCount { get; set; }
    }

    /// <summary>
    /// Types of IP bans
    /// </summary>
    public enum BanType
    {
        /// <summary>
        /// Temporary ban that expires
        /// </summary>
        Temporary,

        /// <summary>
        /// Permanent ban
        /// </summary>
        Permanent,

        /// <summary>
        /// Rate limit triggered ban
        /// </summary>
        RateLimit,

        /// <summary>
        /// Security threat triggered ban
        /// </summary>
        SecurityThreat,

        /// <summary>
        /// Manual administrative ban
        /// </summary>
        Manual,

        /// <summary>
        /// Automated system ban
        /// </summary>
        Automated
    }
}