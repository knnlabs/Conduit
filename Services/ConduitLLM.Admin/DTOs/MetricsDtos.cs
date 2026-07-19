namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Database connection pool metrics for a PostgreSQL database.
    /// </summary>
    public class DatabasePoolMetricsDto
    {
        /// <summary>
        /// UTC timestamp when the metrics were captured.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The database provider name (e.g. "postgresql").
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Non-sensitive connection details extracted from the connection string.
        /// </summary>
        public DatabasePoolConnectionInfoDto ConnectionString { get; set; } = new();

        /// <summary>
        /// Connection pool configuration settings.
        /// </summary>
        public DatabasePoolConfigurationDto PoolConfiguration { get; set; } = new();

        /// <summary>
        /// Current measured pool health metrics.
        /// </summary>
        public DatabasePoolCurrentMetricsDto CurrentMetrics { get; set; } = new();
    }

    /// <summary>
    /// Non-sensitive connection details extracted from the database connection string.
    /// </summary>
    public class DatabasePoolConnectionInfoDto
    {
        /// <summary>
        /// The database server host.
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// The database server port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The database name.
        /// </summary>
        public string? Database { get; set; }

        /// <summary>
        /// The application name reported to the database server.
        /// </summary>
        public string ApplicationName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Connection pool configuration settings.
    /// </summary>
    public class DatabasePoolConfigurationDto
    {
        /// <summary>
        /// Minimum number of connections kept in the pool.
        /// </summary>
        public int MinPoolSize { get; set; }

        /// <summary>
        /// Maximum number of connections allowed in the pool.
        /// </summary>
        public int MaxPoolSize { get; set; }

        /// <summary>
        /// Maximum lifetime of a pooled connection, in seconds.
        /// </summary>
        public int ConnectionLifetime { get; set; }

        /// <summary>
        /// Time before an idle pooled connection is closed, in seconds.
        /// </summary>
        public int ConnectionIdleLifetime { get; set; }

        /// <summary>
        /// Whether connection pooling is enabled.
        /// </summary>
        public bool Pooling { get; set; }
    }

    /// <summary>
    /// Current measured connection pool health metrics.
    /// </summary>
    public class DatabasePoolCurrentMetricsDto
    {
        /// <summary>
        /// Time taken to acquire a connection from the pool, in milliseconds.
        /// </summary>
        public long ConnectionAcquisitionTimeMs { get; set; }

        /// <summary>
        /// Health status derived from the connection acquisition time
        /// ("healthy", "degraded", or "unhealthy").
        /// </summary>
        public string HealthStatus { get; set; } = string.Empty;

        /// <summary>
        /// Additional note about the metrics.
        /// </summary>
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response returned when connection pool metrics are unavailable for the database provider.
    /// </summary>
    public class DatabasePoolMetricsUnavailableDto
    {
        /// <summary>
        /// The database provider name (e.g. "non-postgresql").
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Explanation of why pool metrics are unavailable.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive application metrics including database, application, and system metrics.
    /// </summary>
    public class AllMetricsDto
    {
        /// <summary>
        /// UTC timestamp when the metrics were captured.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Application identity information.
        /// </summary>
        public ApplicationInfoDto Application { get; set; } = new();

        /// <summary>
        /// Database connection pool metrics. Either <see cref="DatabasePoolMetricsDto"/>,
        /// <see cref="DatabasePoolMetricsUnavailableDto"/>, or null when unavailable.
        /// </summary>
        public object? Database { get; set; }

        /// <summary>
        /// Host system metrics for the current process.
        /// </summary>
        public SystemMetricsDto System { get; set; } = new();
    }

    /// <summary>
    /// Application identity information.
    /// </summary>
    public class ApplicationInfoDto
    {
        /// <summary>
        /// The application name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The application assembly version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// The ASP.NET Core environment name.
        /// </summary>
        public string Environment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Host system metrics for the current process.
    /// </summary>
    public class SystemMetricsDto
    {
        /// <summary>
        /// Number of logical processors available.
        /// </summary>
        public int CpuCount { get; set; }

        /// <summary>
        /// Process working set size in megabytes.
        /// </summary>
        public long WorkingSetMb { get; set; }

        /// <summary>
        /// Total memory tracked by the garbage collector in megabytes.
        /// </summary>
        public long GcMemoryMb { get; set; }

        /// <summary>
        /// Number of threads in the current process.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Time elapsed since the process started.
        /// </summary>
        public TimeSpan Uptime { get; set; }
    }
}
