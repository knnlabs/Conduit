namespace ConduitLLM.Configuration.Interceptors;

/// <summary>
/// Configuration options for query monitoring and performance tracking.
/// </summary>
public class QueryMonitoringOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "QueryMonitoring";

    /// <summary>
    /// Gets or sets whether query monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the threshold in milliseconds for logging slow queries.
    /// Queries exceeding this threshold will be logged as warnings.
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the threshold for logging large result sets.
    /// Result sets exceeding this row count will be logged as warnings.
    /// </summary>
    public int LargeResultSetThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to include the full SQL command in log messages.
    /// This can be useful for debugging but may expose sensitive data.
    /// </summary>
    public bool LogFullCommand { get; set; } = false;
}
