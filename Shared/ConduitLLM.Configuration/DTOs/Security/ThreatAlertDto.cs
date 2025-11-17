namespace ConduitLLM.Configuration.DTOs.Security
{
    /// <summary>
    /// Represents a detected security threat
    /// </summary>
    public class ThreatAlertDto
    {
        /// <summary>
        /// Unique identifier for the threat alert
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of threat detected
        /// </summary>
        public ThreatType ThreatType { get; set; }

        /// <summary>
        /// Severity of the threat
        /// </summary>
        public ThreatSeverity Severity { get; set; }

        /// <summary>
        /// When the threat was detected
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Title of the threat alert
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of the threat
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Source IP addresses involved
        /// </summary>
        public List<string> SourceIps { get; set; } = new();

        /// <summary>
        /// Virtual keys involved (if any)
        /// </summary>
        public List<string> AffectedVirtualKeys { get; set; } = new();

        /// <summary>
        /// Endpoints targeted
        /// </summary>
        public List<string> TargetedEndpoints { get; set; } = new();

        /// <summary>
        /// Number of related security events
        /// </summary>
        public int RelatedEventCount { get; set; }

        /// <summary>
        /// Confidence score (0-100)
        /// </summary>
        public int ConfidenceScore { get; set; }

        /// <summary>
        /// Recommended actions to mitigate the threat
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

        /// <summary>
        /// Current status of the threat
        /// </summary>
        public ThreatStatus Status { get; set; } = ThreatStatus.Active;

        /// <summary>
        /// Whether automatic mitigation was applied
        /// </summary>
        public bool AutoMitigated { get; set; }

        /// <summary>
        /// Mitigation actions taken
        /// </summary>
        public List<string> MitigationActions { get; set; } = new();
    }

    /// <summary>
    /// Types of security threats
    /// </summary>
    public enum ThreatType
    {
        /// <summary>
        /// Brute force attack
        /// </summary>
        BruteForce,

        /// <summary>
        /// Distributed attack from multiple IPs
        /// </summary>
        DistributedAttack,

        /// <summary>
        /// Data exfiltration attempt
        /// </summary>
        DataExfiltration,

        /// <summary>
        /// API abuse or excessive usage
        /// </summary>
        ApiAbuse,

        /// <summary>
        /// Suspicious access pattern
        /// </summary>
        SuspiciousPattern,

        /// <summary>
        /// Credential stuffing attack
        /// </summary>
        CredentialStuffing,

        /// <summary>
        /// Anomalous behavior detected
        /// </summary>
        AnomalousBehavior,

        /// <summary>
        /// Malicious payload injection
        /// </summary>
        MaliciousPayload,

        /// <summary>
        /// Unauthorized access attempt
        /// </summary>
        UnauthorizedAccess,

        /// <summary>
        /// Rate limit circumvention
        /// </summary>
        RateLimitBypass
    }

    /// <summary>
    /// Threat severity levels
    /// </summary>
    public enum ThreatSeverity
    {
        /// <summary>
        /// Low severity threat
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity threat
        /// </summary>
        Medium,

        /// <summary>
        /// High severity threat
        /// </summary>
        High,

        /// <summary>
        /// Critical severity threat
        /// </summary>
        Critical
    }

    /// <summary>
    /// Current status of a threat
    /// </summary>
    public enum ThreatStatus
    {
        /// <summary>
        /// Threat is currently active
        /// </summary>
        Active,

        /// <summary>
        /// Threat has been mitigated
        /// </summary>
        Mitigated,

        /// <summary>
        /// Threat is being investigated
        /// </summary>
        Investigating,

        /// <summary>
        /// Threat has been resolved
        /// </summary>
        Resolved,

        /// <summary>
        /// False positive
        /// </summary>
        FalsePositive
    }
}