using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// How a service participates in database schema migration at startup.
    /// </summary>
    public enum MigrationMode
    {
        /// <summary>
        /// Apply pending migrations inline before serving traffic, serialized across
        /// instances by a Postgres advisory lock. Default; the right mode for
        /// development and single-writer deployments.
        /// </summary>
        Apply,

        /// <summary>
        /// Never migrate. Poll until the schema contains every migration this binary
        /// knows about, gating /health/ready in the meantime. The right mode for
        /// production services when a release-hook migrator runs "migrate" separately.
        /// </summary>
        Wait,

        /// <summary>
        /// No migration work and no schema checks. For tests and break-glass operations only.
        /// </summary>
        Skip
    }

    /// <summary>
    /// Migration startup behavior resolved from environment variables.
    /// </summary>
    public sealed class MigrationStartupOptions
    {
        public const string ModeVariable = "CONDUIT_MIGRATION_MODE";
        public const string LockTimeoutVariable = "CONDUIT_MIGRATION_LOCK_TIMEOUT_SECONDS";
        public const string WaitTimeoutVariable = "CONDUIT_MIGRATION_WAIT_TIMEOUT_SECONDS";

        public const int DefaultLockTimeoutSeconds = 300;

        public MigrationMode Mode { get; init; } = MigrationMode.Apply;

        /// <summary>
        /// How long an Apply-mode instance waits for the advisory lock before failing
        /// startup. Only contended waiters can time out — the lock winner holds it for
        /// as long as the migration takes. 0 means wait indefinitely.
        /// </summary>
        public int LockTimeoutSeconds { get; init; } = DefaultLockTimeoutSeconds;

        /// <summary>
        /// How long a Wait-mode instance polls for the schema to become current before
        /// shutting down. 0 (default) means poll forever; readiness stays down meanwhile.
        /// </summary>
        public int WaitTimeoutSeconds { get; init; }

        public static MigrationStartupOptions FromEnvironment(ILogger logger)
        {
            WarnOnRemovedVariables(logger);

            var rawMode = Environment.GetEnvironmentVariable(ModeVariable);
            MigrationMode mode;
            if (string.IsNullOrWhiteSpace(rawMode))
            {
                mode = MigrationMode.Apply;
            }
            else if (!Enum.TryParse(rawMode.Trim(), ignoreCase: true, out mode))
            {
                // Fail fast: silently applying migrations in a misconfigured production
                // service is worse than refusing to start.
                throw new InvalidOperationException(
                    $"Unrecognized {ModeVariable} value '{rawMode}'. Valid values: Apply, Wait, Skip.");
            }

            return new MigrationStartupOptions
            {
                Mode = mode,
                LockTimeoutSeconds = ParseNonNegativeSeconds(LockTimeoutVariable, DefaultLockTimeoutSeconds),
                WaitTimeoutSeconds = ParseNonNegativeSeconds(WaitTimeoutVariable, 0)
            };
        }

        private static int ParseNonNegativeSeconds(string variable, int defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (!int.TryParse(raw.Trim(), out var seconds) || seconds < 0)
            {
                throw new InvalidOperationException(
                    $"{variable} must be a non-negative integer number of seconds; got '{raw}'.");
            }

            return seconds;
        }

        private static void WarnOnRemovedVariables(ILogger logger)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT")))
            {
                logger.LogWarning(
                    "CONDUIT_SKIP_DATABASE_INIT has been REMOVED and is ignored. " +
                    "Use {ModeVariable}=Skip (tests) or {ModeVariable}=Wait (production services) instead.",
                    ModeVariable, ModeVariable);
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FORCE_RECREATE_DB_ON_FAILURE")))
            {
                logger.LogWarning(
                    "FORCE_RECREATE_DB_ON_FAILURE has been REMOVED and is ignored. " +
                    "Failed migrations now always fail startup; recreate development databases explicitly " +
                    "(e.g. ./scripts/dev.ps1 -Clean).");
            }
        }
    }
}
