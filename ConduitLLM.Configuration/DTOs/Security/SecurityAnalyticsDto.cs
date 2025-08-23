namespace ConduitLLM.Configuration.DTOs.Security
{
    /// <summary>
    /// Represents a distributed attack detection
    /// </summary>
    public class DistributedAttackDto
    {
        /// <summary>
        /// Attack identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of distributed attack
        /// </summary>
        public string AttackType { get; set; } = string.Empty;

        /// <summary>
        /// List of participating IP addresses
        /// </summary>
        public List<string> ParticipatingIps { get; set; } = new();

        /// <summary>
        /// Targeted endpoints
        /// </summary>
        public List<string> TargetedEndpoints { get; set; } = new();

        /// <summary>
        /// When the attack started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the attack ended (if applicable)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Total number of requests in the attack
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Attack pattern signature
        /// </summary>
        public string PatternSignature { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score (0-100)
        /// </summary>
        public int ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Represents an anomaly detection
    /// </summary>
    public class AnomalyDto
    {
        /// <summary>
        /// Anomaly identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of anomaly detected
        /// </summary>
        public string AnomalyType { get; set; } = string.Empty;

        /// <summary>
        /// Description of the anomaly
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// When the anomaly was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>
        /// Severity of the anomaly
        /// </summary>
        public AnomalySeverity Severity { get; set; }

        /// <summary>
        /// Affected resource (virtual key, endpoint, etc.)
        /// </summary>
        public string AffectedResource { get; set; } = string.Empty;

        /// <summary>
        /// Deviation from normal behavior (percentage)
        /// </summary>
        public double DeviationPercentage { get; set; }

        /// <summary>
        /// Supporting evidence
        /// </summary>
        public Dictionary<string, object> Evidence { get; set; } = new();
    }

    /// <summary>
    /// Aggregated security metrics over a time period
    /// </summary>
    public class AggregatedSecurityMetricsDto
    {
        /// <summary>
        /// Start of the aggregation period
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End of the aggregation period
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Total security events
        /// </summary>
        public long TotalEvents { get; set; }

        /// <summary>
        /// Events by type
        /// </summary>
        public Dictionary<string, long> EventsByType { get; set; } = new();

        /// <summary>
        /// Events by severity
        /// </summary>
        public Dictionary<string, long> EventsBySeverity { get; set; } = new();

        /// <summary>
        /// Top threat sources
        /// </summary>
        public List<ThreatSourceDto> TopThreatSources { get; set; } = new();

        /// <summary>
        /// Average threat level
        /// </summary>
        public double AverageThreatLevel { get; set; }

        /// <summary>
        /// Peak threat periods
        /// </summary>
        public List<PeakThreatPeriodDto> PeakPeriods { get; set; } = new();
    }

    /// <summary>
    /// Component-specific security metrics
    /// </summary>
    public class ComponentSecurityMetricsDto
    {
        /// <summary>
        /// Component name
        /// </summary>
        public string ComponentName { get; set; } = string.Empty;

        /// <summary>
        /// Number of security events
        /// </summary>
        public int SecurityEventCount { get; set; }

        /// <summary>
        /// Number of blocked requests
        /// </summary>
        public int BlockedRequests { get; set; }

        /// <summary>
        /// Number of suspicious activities
        /// </summary>
        public int SuspiciousActivities { get; set; }

        /// <summary>
        /// Component health status
        /// </summary>
        public string HealthStatus { get; set; } = string.Empty;

        /// <summary>
        /// Last security incident
        /// </summary>
        public DateTime? LastIncident { get; set; }
    }

    /// <summary>
    /// Security trends over time
    /// </summary>
    public class SecurityTrendsDto
    {
        /// <summary>
        /// Trend period in days
        /// </summary>
        public int PeriodDays { get; set; }

        /// <summary>
        /// Daily event counts
        /// </summary>
        public List<DailySecurityMetricsDto> DailyMetrics { get; set; } = new();

        /// <summary>
        /// Overall trend direction
        /// </summary>
        public TrendDirection OverallTrend { get; set; }

        /// <summary>
        /// Percentage change in threats
        /// </summary>
        public double ThreatChangePercentage { get; set; }

        /// <summary>
        /// Emerging threat patterns
        /// </summary>
        public List<string> EmergingPatterns { get; set; } = new();
    }

    /// <summary>
    /// Top security threats
    /// </summary>
    public class TopThreatDto
    {
        /// <summary>
        /// Threat identifier
        /// </summary>
        public string ThreatId { get; set; } = string.Empty;

        /// <summary>
        /// Threat name/type
        /// </summary>
        public string ThreatName { get; set; } = string.Empty;

        /// <summary>
        /// Number of occurrences
        /// </summary>
        public int OccurrenceCount { get; set; }

        /// <summary>
        /// Threat severity
        /// </summary>
        public ThreatSeverity Severity { get; set; }

        /// <summary>
        /// Risk score (0-100)
        /// </summary>
        public int RiskScore { get; set; }

        /// <summary>
        /// Last occurrence
        /// </summary>
        public DateTime LastOccurrence { get; set; }

        /// <summary>
        /// Affected resources
        /// </summary>
        public List<string> AffectedResources { get; set; } = new();
    }

    /// <summary>
    /// Security compliance metrics
    /// </summary>
    public class ComplianceMetricsDto
    {
        /// <summary>
        /// Overall compliance score (0-100)
        /// </summary>
        public double ComplianceScore { get; set; }

        /// <summary>
        /// Compliance status by category
        /// </summary>
        public Dictionary<string, ComplianceStatusDto> CategoryStatus { get; set; } = new();

        /// <summary>
        /// Failed compliance checks
        /// </summary>
        public List<ComplianceViolationDto> Violations { get; set; } = new();

        /// <summary>
        /// Last compliance check
        /// </summary>
        public DateTime LastCheckTime { get; set; }

        /// <summary>
        /// Next scheduled check
        /// </summary>
        public DateTime NextCheckTime { get; set; }
    }

    /// <summary>
    /// Supporting DTOs
    /// </summary>
    public class ThreatSourceDto
    {
        public string Source { get; set; } = string.Empty;
        public int ThreatCount { get; set; }
        public List<string> ThreatTypes { get; set; } = new();
    }

    public class PeakThreatPeriodDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int EventCount { get; set; }
        public ThreatLevel MaxThreatLevel { get; set; }
    }

    public class DailySecurityMetricsDto
    {
        public DateTime Date { get; set; }
        public int TotalEvents { get; set; }
        public Dictionary<string, int> EventsByType { get; set; } = new();
        public double AverageThreatLevel { get; set; }
    }

    public class ComplianceStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class ComplianceViolationDto
    {
        public string Rule { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }

    /// <summary>
    /// Anomaly severity levels
    /// </summary>
    public enum AnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Trend direction
    /// </summary>
    public enum TrendDirection
    {
        Decreasing,
        Stable,
        Increasing,
        Spike
    }
}