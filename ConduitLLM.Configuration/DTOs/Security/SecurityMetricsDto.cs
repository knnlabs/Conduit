namespace ConduitLLM.Configuration.DTOs.Security
{
    /// <summary>
    /// Security metrics summary
    /// </summary>
    public class SecurityMetricsDto
    {
        /// <summary>
        /// Total number of security events
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// Number of authentication failures
        /// </summary>
        public int AuthenticationFailures { get; set; }

        /// <summary>
        /// Number of rate limit violations
        /// </summary>
        public int RateLimitViolations { get; set; }

        /// <summary>
        /// Number of suspicious activities detected
        /// </summary>
        public int SuspiciousActivities { get; set; }

        /// <summary>
        /// Number of currently active IP bans
        /// </summary>
        public int ActiveIpBans { get; set; }

        /// <summary>
        /// Number of unique IPs being monitored
        /// </summary>
        public int UniqueIpsMonitored { get; set; }

        /// <summary>
        /// Number of unique virtual keys being monitored
        /// </summary>
        public int UniqueKeysMonitored { get; set; }

        /// <summary>
        /// Number of data exfiltration attempts
        /// </summary>
        public int DataExfiltrationAttempts { get; set; }

        /// <summary>
        /// Number of anomalous access patterns detected
        /// </summary>
        public int AnomalousAccessPatterns { get; set; }

        /// <summary>
        /// Current overall threat level
        /// </summary>
        public ThreatLevel ThreatLevel { get; set; }

        /// <summary>
        /// Time period for these metrics
        /// </summary>
        public DateTime MetricsStartTime { get; set; }

        /// <summary>
        /// End time for these metrics
        /// </summary>
        public DateTime MetricsEndTime { get; set; }

        /// <summary>
        /// Metrics by event type
        /// </summary>
        public Dictionary<string, int> EventTypeBreakdown { get; set; } = new();

        /// <summary>
        /// Top attacking IPs
        /// </summary>
        public List<IpThreatInfo> TopThreats { get; set; } = new();
    }

    /// <summary>
    /// Overall threat level assessment
    /// </summary>
    public enum ThreatLevel
    {
        /// <summary>
        /// No threats detected
        /// </summary>
        None = 0,

        /// <summary>
        /// Low threat level
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium threat level
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High threat level
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical threat level
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Information about a threatening IP
    /// </summary>
    public class IpThreatInfo
    {
        /// <summary>
        /// IP address
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Number of threats from this IP
        /// </summary>
        public int ThreatCount { get; set; }

        /// <summary>
        /// Types of threats detected
        /// </summary>
        public List<string> ThreatTypes { get; set; } = new();

        /// <summary>
        /// Risk score (0-100)
        /// </summary>
        public int RiskScore { get; set; }

        /// <summary>
        /// Whether the IP is currently banned
        /// </summary>
        public bool IsBanned { get; set; }

        /// <summary>
        /// Last activity from this IP
        /// </summary>
        public DateTime LastActivity { get; set; }
    }
}