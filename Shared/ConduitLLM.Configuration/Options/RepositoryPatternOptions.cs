namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for the repository pattern implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class defines options for controlling the repository pattern usage across different
    /// environments and provides fine-grained control for phased rollout and performance monitoring.
    /// </para>
    /// <para>
    /// The repository pattern can be enabled globally or for specific environments, and additional
    /// configuration options control logging, performance tracking, and fallback behavior.
    /// </para>
    /// </remarks>
    public class RepositoryPatternOptions
    {
        /// <summary>
        /// Gets the configuration section name for repository pattern options.
        /// </summary>
        public static readonly string SectionName = "RepositoryPattern";

        /// <summary>
        /// Gets or sets whether the repository pattern is enabled.
        /// </summary>
        /// <remarks>
        /// This is the master toggle for the repository pattern. When false, the legacy implementation
        /// will be used regardless of other settings.
        /// </remarks>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets a comma-separated list of environments where the repository pattern should be enabled.
        /// </summary>
        /// <remarks>
        /// If specified, the repository pattern will only be enabled in the listed environments,
        /// and only if <see cref="Enabled"/> is also true. Examples: "Staging,Canary,Production"
        /// </remarks>
        public string? EnabledEnvironments { get; set; }

        /// <summary>
        /// Gets or sets whether detailed logging is enabled for repository pattern operations.
        /// </summary>
        /// <remarks>
        /// When true, additional diagnostic logging will be performed for repository operations,
        /// which is useful during migration and testing phases.
        /// </remarks>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets whether performance metrics should be collected for repository operations.
        /// </summary>
        /// <remarks>
        /// When true, the system will track and record performance metrics for repository operations,
        /// enabling comparison between legacy and repository pattern implementations.
        /// </remarks>
        public bool TrackPerformanceMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to run repository operations in parallel with legacy operations for verification.
        /// </summary>
        /// <remarks>
        /// When true, both implementations will run simultaneously during the migration phase,
        /// allowing for verification of results without affecting the user experience.
        /// This is a testing feature and should be disabled in production.
        /// </remarks>
        public bool EnableParallelVerification { get; set; } = false;

        /// <summary>
        /// Determines if the repository pattern should be enabled for the current environment.
        /// </summary>
        /// <param name="currentEnvironment">The current environment name.</param>
        /// <returns>True if the repository pattern should be enabled, false otherwise.</returns>
        public bool IsEnabledForEnvironment(string currentEnvironment)
        {
            // If the master toggle is off, always return false
            if (!Enabled)
            {
                return false;
            }

            // If no specific environments are defined, use the master toggle value
            if (string.IsNullOrWhiteSpace(EnabledEnvironments))
            {
                return true;
            }

            // Check if the current environment is in the enabled list
            var environments = EnabledEnvironments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Array.Exists(environments, env => string.Equals(env, currentEnvironment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
