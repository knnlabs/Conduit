namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Represents a health check response from the API.
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Gets or sets the overall health status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total duration of all health checks in milliseconds.
    /// </summary>
    public double TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the list of individual health check results.
    /// </summary>
    public List<HealthCheckItem> Checks { get; set; } = new();
}

/// <summary>
/// Represents an individual health check item.
/// </summary>
public class HealthCheckItem
{
    /// <summary>
    /// Gets or sets the name of the health check.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of this health check.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the health check result.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the duration of this health check in milliseconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Gets or sets additional data associated with this health check.
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Gets or sets any exception information if the health check failed.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Gets or sets tags associated with this health check.
    /// </summary>
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Health check status enumeration.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The component is healthy.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// The component is degraded but still functioning.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// The component is unhealthy.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Health check options for customizing health check behavior.
/// </summary>
public class HealthCheckOptions
{
    /// <summary>
    /// Gets or sets the timeout for health checks.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets whether to include exception details in the response.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = false;

    /// <summary>
    /// Gets or sets tags to filter health checks by.
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets the failure status to return if checks fail.
    /// </summary>
    public HealthStatus FailureStatus { get; set; } = HealthStatus.Unhealthy;
}

/// <summary>
/// Simplified health status for quick checks.
/// </summary>
public class SimpleHealthStatus
{
    /// <summary>
    /// Gets or sets whether the system is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets a brief status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the health check.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public double ResponseTimeMs { get; set; }
}