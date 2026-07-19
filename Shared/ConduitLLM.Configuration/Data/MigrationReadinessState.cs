namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Process-wide flag: does the database schema contain every migration this binary
    /// knows about? Set before the server binds in Apply/Skip modes; flipped by
    /// <see cref="MigrationWaitService"/> in Wait mode. Gates /health/ready via
    /// <see cref="HealthChecks.PendingMigrationsReadinessCheck"/>.
    /// </summary>
    public sealed class MigrationReadinessState
    {
        private volatile bool _isSchemaCurrent;

        public bool IsSchemaCurrent
        {
            get => _isSchemaCurrent;
            set => _isSchemaCurrent = value;
        }
    }
}
