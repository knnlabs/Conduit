namespace ConduitLLM.TUI.Constants;

/// <summary>
/// Connection state enumeration.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Service is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Service is connecting.
    /// </summary>
    Connecting,

    /// <summary>
    /// Service is connected.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection has failed.
    /// </summary>
    Failed
}

/// <summary>
/// Task status enumeration.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// Task is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Task has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Task has failed.
    /// </summary>
    Failed
}

/// <summary>
/// Change type enumeration.
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// Entity was created.
    /// </summary>
    Created,

    /// <summary>
    /// Entity was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// Entity was deleted.
    /// </summary>
    Deleted
}

/// <summary>
/// Priority level enumeration.
/// </summary>
public enum PriorityLevel
{
    /// <summary>
    /// Low priority.
    /// </summary>
    Low,

    /// <summary>
    /// Medium priority.
    /// </summary>
    Medium,

    /// <summary>
    /// High priority.
    /// </summary>
    High
}

/// <summary>
/// Configuration tab type enumeration.
/// </summary>
public enum ConfigurationTabType
{
    /// <summary>
    /// Global settings tab.
    /// </summary>
    Global,

    /// <summary>
    /// HTTP client configuration tab.
    /// </summary>
    HttpClient,

    /// <summary>
    /// Cache configuration tab.
    /// </summary>
    Cache,

    /// <summary>
    /// Router configuration tab.
    /// </summary>
    Router,

    /// <summary>
    /// IP filter configuration tab.
    /// </summary>
    IpFilter,

    /// <summary>
    /// Audio configuration tab.
    /// </summary>
    Audio
}