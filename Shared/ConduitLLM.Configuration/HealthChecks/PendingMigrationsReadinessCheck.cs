using ConduitLLM.Configuration.Data;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Fails readiness until <see cref="MigrationReadinessState"/> reports the schema
    /// current. Must be registered with the "ready" tag — /health/ready filters on it.
    /// In Apply/Skip modes the state is set before the server binds, so this check
    /// never fails there; in Wait mode it holds readiness at 503 until the external
    /// migrator has applied all migrations this binary knows about.
    /// </summary>
    public class PendingMigrationsReadinessCheck : IHealthCheck
    {
        private readonly MigrationReadinessState _state;

        public PendingMigrationsReadinessCheck(MigrationReadinessState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_state.IsSchemaCurrent
                ? HealthCheckResult.Healthy("Database schema is current")
                : HealthCheckResult.Unhealthy(
                    "Database schema is not current; waiting for migrations to be applied"));
        }
    }
}
