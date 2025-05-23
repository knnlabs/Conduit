namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for retrieving system information through the Admin API
/// </summary>
public interface IAdminSystemInfoService
{
    /// <summary>
    /// Gets system information
    /// </summary>
    /// <returns>System information details</returns>
    Task<SystemInfoDto> GetSystemInfoAsync();
    
    /// <summary>
    /// Gets system health status
    /// </summary>
    /// <returns>Health status information</returns>
    Task<HealthStatusDto> GetHealthStatusAsync();
}

/// <summary>
/// DTO containing system information
/// </summary>
public class SystemInfoDto
{
    /// <summary>
    /// ConduitLLM version information
    /// </summary>
    public VersionInfo Version { get; set; } = new();
    
    /// <summary>
    /// Operating system information
    /// </summary>
    public OsInfo OperatingSystem { get; set; } = new();
    
    /// <summary>
    /// Database information
    /// </summary>
    public DatabaseInfo Database { get; set; } = new();
    
    /// <summary>
    /// Runtime information
    /// </summary>
    public RuntimeInfo Runtime { get; set; } = new();
    
    /// <summary>
    /// Record counts from database tables
    /// </summary>
    public RecordCountsDto RecordCounts { get; set; } = new();
}

/// <summary>
/// Version information
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// Application version
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Build date
    /// </summary>
    public DateTime? BuildDate { get; set; }
}

/// <summary>
/// Operating system information
/// </summary>
public class OsInfo
{
    /// <summary>
    /// Operating system description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Process architecture
    /// </summary>
    public string Architecture { get; set; } = string.Empty;
}

/// <summary>
/// Database information
/// </summary>
public class DatabaseInfo
{
    /// <summary>
    /// Database provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
    
    /// <summary>
    /// Database version
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Connection status
    /// </summary>
    public bool Connected { get; set; }
    
    /// <summary>
    /// Masked connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Database file location (for file-based databases)
    /// </summary>
    public string Location { get; set; } = string.Empty;
    
    /// <summary>
    /// Database size
    /// </summary>
    public string Size { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of tables in the database
    /// </summary>
    public int TableCount { get; set; }
}

/// <summary>
/// Runtime information
/// </summary>
public class RuntimeInfo
{
    /// <summary>
    /// .NET runtime version
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Process start time
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Total process uptime
    /// </summary>
    public TimeSpan Uptime { get; set; }
}

/// <summary>
/// DTO containing health status information
/// </summary>
public class HealthStatusDto
{
    /// <summary>
    /// Overall system status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Individual component health statuses
    /// </summary>
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
}

/// <summary>
/// Component health information
/// </summary>
public class ComponentHealth
{
    /// <summary>
    /// Component status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Component description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional health details
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = new();
}

/// <summary>
/// Record counts from database tables
/// </summary>
public class RecordCountsDto
{
    /// <summary>
    /// Number of virtual keys
    /// </summary>
    public int VirtualKeys { get; set; }
    
    /// <summary>
    /// Number of request logs
    /// </summary>
    public int Requests { get; set; }
    
    /// <summary>
    /// Number of global settings
    /// </summary>
    public int Settings { get; set; }
    
    /// <summary>
    /// Number of provider credentials
    /// </summary>
    public int Providers { get; set; }
    
    /// <summary>
    /// Number of model provider mappings
    /// </summary>
    public int ModelMappings { get; set; }
}