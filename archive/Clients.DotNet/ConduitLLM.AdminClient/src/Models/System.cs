namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents comprehensive system information.
/// </summary>
public class SystemInfoDto
{
    /// <summary>
    /// Gets or sets version information.
    /// </summary>
    public VersionInfo Version { get; set; } = new();

    /// <summary>
    /// Gets or sets operating system information.
    /// </summary>
    public OsInfo OperatingSystem { get; set; } = new();

    /// <summary>
    /// Gets or sets database information.
    /// </summary>
    public DatabaseInfo Database { get; set; } = new();

    /// <summary>
    /// Gets or sets runtime information.
    /// </summary>
    public RuntimeInfo Runtime { get; set; } = new();

    /// <summary>
    /// Gets or sets record counts for various database tables.
    /// </summary>
    public RecordCountsDto RecordCounts { get; set; } = new();
}

/// <summary>
/// Represents version information for the application.
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the build date.
    /// </summary>
    public DateTime BuildDate { get; set; }

    /// <summary>
    /// Gets or sets the commit hash.
    /// </summary>
    public string? CommitHash { get; set; }

    /// <summary>
    /// Gets or sets the branch name.
    /// </summary>
    public string? Branch { get; set; }
}

/// <summary>
/// Represents operating system information.
/// </summary>
public class OsInfo
{
    /// <summary>
    /// Gets or sets the OS description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OS architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OS version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;
}

/// <summary>
/// Represents database information.
/// </summary>
public class DatabaseInfo
{
    /// <summary>
    /// Gets or sets the database provider.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection status.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the connection string (sanitized).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database size information.
    /// </summary>
    public long? SizeBytes { get; set; }
}

/// <summary>
/// Represents runtime information.
/// </summary>
public class RuntimeInfo
{
    /// <summary>
    /// Gets or sets the .NET version.
    /// </summary>
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the application uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the current working directory.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the garbage collection information.
    /// </summary>
    public GcInfo GarbageCollection { get; set; } = new();
}

/// <summary>
/// Represents garbage collection information.
/// </summary>
public class GcInfo
{
    /// <summary>
    /// Gets or sets the total allocated memory.
    /// </summary>
    public long TotalAllocatedBytes { get; set; }

    /// <summary>
    /// Gets or sets the current memory usage.
    /// </summary>
    public long CurrentMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the generation 0 collection count.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gets or sets the generation 1 collection count.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gets or sets the generation 2 collection count.
    /// </summary>
    public int Gen2Collections { get; set; }
}

/// <summary>
/// Represents record counts for database tables.
/// </summary>
public class RecordCountsDto
{
    /// <summary>
    /// Gets or sets the number of virtual keys.
    /// </summary>
    public int VirtualKeys { get; set; }

    /// <summary>
    /// Gets or sets the number of provider credentials.
    /// </summary>
    public int ProviderCredentials { get; set; }

    /// <summary>
    /// Gets or sets the number of model mappings.
    /// </summary>
    public int ModelMappings { get; set; }

    /// <summary>
    /// Gets or sets the number of usage logs.
    /// </summary>
    public int UsageLogs { get; set; }

    /// <summary>
    /// Gets or sets the number of model costs.
    /// </summary>
    public int ModelCosts { get; set; }

    /// <summary>
    /// Gets or sets the number of IP filters.
    /// </summary>
    public int IpFilters { get; set; }

    /// <summary>
    /// Gets or sets the number of global settings.
    /// </summary>
    public int GlobalSettings { get; set; }
}

/// <summary>
/// Represents overall health status of the system.
/// </summary>
public class HealthStatusDto
{
    /// <summary>
    /// Gets or sets the overall system status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the health check.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the overall description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the health status of individual components.
    /// </summary>
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();

    /// <summary>
    /// Gets or sets the total duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Represents the health status of an individual component.
/// </summary>
public class ComponentHealth
{
    /// <summary>
    /// Gets or sets the component status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the component description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets any error message.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the duration of the component health check.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets additional data about the component.
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Represents health status values.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// System is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// System is degraded but functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// System is unhealthy.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Health status is unknown.
    /// </summary>
    Unknown
}


