namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Key insight from analytics.
    /// </summary>
    public class KeyInsight
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InsightSeverity Severity { get; set; }
        public string? Recommendation { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Insight severity levels.
    /// </summary>
    public enum InsightSeverity
    {
        Info,
        Opportunity,
        Warning,
        Critical
    }

    /// <summary>
    /// Cost optimization opportunity.
    /// </summary>
    public class CostOptimizationOpportunity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PotentialSavings { get; set; }
        public string Implementation { get; set; } = string.Empty;
        public OpportunityImpact Impact { get; set; }
        public OpportunityEffort Effort { get; set; }
    }

    /// <summary>
    /// Impact level of optimization opportunity.
    /// </summary>
    public enum OpportunityImpact
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Implementation effort required.
    /// </summary>
    public enum OpportunityEffort
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Cost anomaly detection.
    /// </summary>
    public class CostAnomaly
    {
        public DateTime DetectedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal AnomalousAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public double DeviationPercent { get; set; }
    }

    /// <summary>
    /// Error pattern analysis.
    /// </summary>
    public class ErrorPattern
    {
        public string ErrorType { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public double Frequency { get; set; }
        public List<string> AffectedProviders { get; set; } = new();
        public string Pattern { get; set; } = string.Empty;
        public bool IsRetryable { get; set; }
    }

    /// <summary>
    /// Error correlation finding.
    /// </summary>
    public class ErrorCorrelation
    {
        public string Factor { get; set; } = string.Empty;
        public double CorrelationStrength { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual error occurrence.
    /// </summary>
    public class ErrorOccurrence
    {
        public DateTime Timestamp { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Capacity recommendation.
    /// </summary>
    public class CapacityRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CapacityAction Action { get; set; }
        public DateTime RecommendedBy { get; set; }
        public string Justification { get; set; } = string.Empty;
    }

    /// <summary>
    /// Capacity action type.
    /// </summary>
    public enum CapacityAction
    {
        ScaleUp,
        ScaleOut,
        OptimizeUsage,
        AddProvider,
        ImplementCaching,
        EnableRateLimiting
    }

    /// <summary>
    /// Provider capacity information.
    /// </summary>
    public class ProviderCapacity
    {
        public string ProviderName { get; set; } = string.Empty;
        public int RateLimitPerMinute { get; set; }
        public double CurrentUtilization { get; set; }
        public int AvailableCapacity { get; set; }
        public bool IsAtCapacity { get; set; }
    }

    /// <summary>
    /// Individual virtual key usage.
    /// </summary>
    public class VirtualKeyUsage
    {
        public int VirtualKeyId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int TotalImages { get; set; }
        public decimal TotalCost { get; set; }
        public decimal BudgetRemaining { get; set; }
        public double BudgetUtilization { get; set; }
        public DateTime LastUsed { get; set; }
        public Dictionary<string, int> RequestsByProvider { get; set; } = new();
    }

    /// <summary>
    /// Usage trend for a virtual key.
    /// </summary>
    public class UsageTrend
    {
        public ImageGenerationTrendDirection Direction { get; set; }
        public double GrowthRate { get; set; }
        public DateTime ProjectedBudgetExhaustion { get; set; }
    }

    /// <summary>
    /// Budget alert for virtual key.
    /// </summary>
    public class BudgetAlert
    {
        public int VirtualKeyId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public double BudgetUtilization { get; set; }
        public DateTime ProjectedExhaustion { get; set; }
    }

    /// <summary>
    /// Performance anomaly detection.
    /// </summary>
    public class PerformanceAnomaly
    {
        public DateTime DetectedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double AnomalyScore { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public string? PossibleCause { get; set; }
    }

    /// <summary>
    /// Anomaly pattern.
    /// </summary>
    public class AnomalyPattern
    {
        public string PatternType { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public double RecurrenceProbability { get; set; }
    }

    /// <summary>
    /// Anomaly summary.
    /// </summary>
    public class AnomalySummary
    {
        public int TotalAnomalies { get; set; }
        public int CriticalAnomalies { get; set; }
        public double SystemStability { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }
}