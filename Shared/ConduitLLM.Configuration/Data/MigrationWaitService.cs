using System.Diagnostics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Read-only probe for migrations known to this binary but not yet applied to the database.
    /// </summary>
    public interface IPendingMigrationsProbe
    {
        Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Probes pending migrations through the pooled context factory.
    /// </summary>
    public sealed class PendingMigrationsProbe : IPendingMigrationsProbe
    {
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;

        public PendingMigrationsProbe(IDbContextFactory<ConduitDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        }
    }

    /// <summary>
    /// In Wait mode, polls until the schema contains every migration this binary knows
    /// about (an external migrator — the "migrate" verb — applies them), then flips
    /// <see cref="MigrationReadinessState"/> so /health/ready starts passing. No-op in
    /// Skip mode. An unreachable database is not fatal: readiness simply stays
    /// down with an actionable diagnostic, which is the correct signal for orchestrators.
    /// </summary>
    public sealed class MigrationWaitService : BackgroundService
    {
        private static readonly TimeSpan InitialPollInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxPollInterval = TimeSpan.FromSeconds(15);

        private readonly MigrationStartupOptions _options;
        private readonly IPendingMigrationsProbe _probe;
        private readonly MigrationReadinessState _state;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<MigrationWaitService> _logger;

        public MigrationWaitService(
            MigrationStartupOptions options,
            IPendingMigrationsProbe probe,
            MigrationReadinessState state,
            IHostApplicationLifetime lifetime,
            ILogger<MigrationWaitService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.Mode != MigrationMode.Wait)
            {
                return;
            }

            // Yield so host startup (and Kestrel binding) proceeds while we poll —
            // /health/ready must be reachable and failing during the wait.
            await Task.Yield();

            _logger.LogInformation(
                "This service never applies database migrations. Polling until the schema is current; " +
                "/health/ready is gated until then. Run 'dotnet ConduitLLM.Admin.dll migrate' before rollout.");

            var elapsed = Stopwatch.StartNew();
            var interval = InitialPollInterval;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var pending = await _probe.GetPendingMigrationsAsync(stoppingToken);
                    if (pending.Count == 0)
                    {
                        _state.IsSchemaCurrent = true;
                        _logger.LogInformation("Database schema is current. Service is ready.");
                        return;
                    }

                    _logger.LogInformation(
                        "Waiting for {PendingCount} pending migration(s) (next: {NextMigration}). " +
                        "Run 'dotnet ConduitLLM.Admin.dll migrate' before rollout.",
                        pending.Count, pending[0]);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check pending migrations; will retry. Readiness remains down.");
                }

                if (_options.WaitTimeoutSeconds > 0 && elapsed.Elapsed.TotalSeconds >= _options.WaitTimeoutSeconds)
                {
                    _logger.LogCritical(
                        "Schema did not become current within {WaitTimeoutVariable}={TimeoutSeconds}s. " +
                        "Run 'dotnet ConduitLLM.Admin.dll migrate', then restart the service. Stopping application.",
                        MigrationStartupOptions.WaitTimeoutVariable, _options.WaitTimeoutSeconds);
                    _lifetime.StopApplication();
                    return;
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (interval < MaxPollInterval)
                {
                    interval = TimeSpan.FromSeconds(Math.Min(interval.TotalSeconds + 2.5, MaxPollInterval.TotalSeconds));
                }
            }
        }
    }
}
