namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Alert types
    /// </summary>
    public enum AlertType
    {
        ServiceDown,
        ServiceDegraded,
        PerformanceDegradation,
        ResourceExhaustion,
        SecurityEvent,
        ConfigurationChange,
        ThresholdBreach,
        ConnectivityIssue,
        DataIntegrity,
        Custom
    }

    /// <summary>
    /// Alert states
    /// </summary>
    public enum AlertState
    {
        Active,
        Acknowledged,
        Resolved,
        Suppressed,
        Expired
    }

    /// <summary>
    /// Health status
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }

    /// <summary>
    /// Comparison operators for alert rules
    /// </summary>
    public enum ComparisonOperator
    {
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equal,
        NotEqual
    }
}