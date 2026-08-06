namespace ConduitLLM.Admin.DTOs
{
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
