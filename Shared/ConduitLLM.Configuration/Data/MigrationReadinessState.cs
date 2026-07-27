namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Process-wide flag: does the database schema contain every migration this binary
    /// knows about? Initially current only in explicit Skip mode and otherwise flipped
    /// by <see cref="MigrationWaitService"/>. Gates /health/ready via
    /// <see cref="HealthChecks.PendingMigrationsReadinessCheck"/>.
    /// </summary>
    public sealed class MigrationReadinessState
    {
        private volatile bool _isSchemaCurrent;

        public MigrationReadinessState(MigrationStartupOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _isSchemaCurrent = options.Mode == MigrationMode.Skip;
        }

        public bool IsSchemaCurrent
        {
            get => _isSchemaCurrent;
            set => _isSchemaCurrent = value;
        }
    }
}
