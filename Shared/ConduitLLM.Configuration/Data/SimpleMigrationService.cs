using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Applies EF Core migrations under a blocking Postgres advisory lock.
    ///
    /// The lock, the migration, and the default-data seed all share one dedicated
    /// NpgsqlConnection (session): if the process dies mid-migration the connection
    /// drops and Postgres releases the lock automatically, so there are no stale
    /// locks and no need to probe __EFMigrationsHistory. Contending instances block
    /// server-side on pg_advisory_lock, wake when the winner finishes, re-check
    /// pending migrations, and find nothing to do.
    /// </summary>
    public class SimpleMigrationService
    {
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;
        private readonly MigrationStartupOptions _options;
        private readonly ILogger<SimpleMigrationService> _logger;

        // PostgreSQL advisory lock ID for migrations — ensures only one instance
        // migrates at a time.
        private const long MIGRATION_LOCK_ID = 7891011;

        public SimpleMigrationService(
            IDbContextFactory<ConduitDbContext> contextFactory,
            MigrationStartupOptions options,
            ILogger<SimpleMigrationService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Acquire the migration lock, apply pending migrations, seed default data, release.
        /// Throws on failure — callers treat any exception as "do not start".
        /// </summary>
        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            var instanceId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{InstanceId}] Starting database migration", instanceId);

            string? connectionString;
            await using (var refContext = await _contextFactory.CreateDbContextAsync(cancellationToken))
            {
                connectionString = refContext.Database.GetConnectionString();
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("No database connection string is configured.");
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await AcquireMigrationLockAsync(connection, instanceId, cancellationToken);
            try
            {
                // The migration context runs over the lock-holding connection so the
                // lock and the schema changes share one session. Deliberately no retry
                // strategy here: a retry hopping to a fresh pooled connection would not
                // hold the lock.
                var contextOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                    .UseNpgsql(connection)
                    .Options;
                await using var context = new ConduitDbContext(contextOptions);

                var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count == 0)
                {
                    _logger.LogInformation(
                        "[{InstanceId}] No pending migrations (another instance may have applied them)", instanceId);
                }
                else
                {
                    _logger.LogInformation(
                        "[{InstanceId}] Applying {PendingCount} pending migration(s), starting with {FirstMigration}",
                        instanceId, pending.Count, pending[0]);
                    await context.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("[{InstanceId}] Migrations completed successfully", instanceId);
                }

                await SeedDefaultDataAsync(context, instanceId, cancellationToken);
            }
            finally
            {
                await ReleaseMigrationLockAsync(connection, instanceId);
            }
        }

        private async Task AcquireMigrationLockAsync(
            NpgsqlConnection connection,
            string instanceId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[{InstanceId}] Acquiring migration advisory lock (blocks if another instance is migrating; timeout {TimeoutSeconds}s)",
                instanceId, _options.LockTimeoutSeconds);

            await using var cmd = new NpgsqlCommand("SELECT pg_advisory_lock(@lockId)", connection);
            cmd.Parameters.AddWithValue("lockId", MIGRATION_LOCK_ID);
            // Blocks server-side until the lock is granted; CommandTimeout aborts
            // waiters that never get it (0 = wait indefinitely). The winner acquires
            // instantly, so only contended waiters can time out.
            cmd.CommandTimeout = _options.LockTimeoutSeconds;

            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (NpgsqlException ex)
            {
                throw new TimeoutException(
                    $"[{instanceId}] Timed out after {_options.LockTimeoutSeconds}s waiting for the migration advisory lock. " +
                    $"Another instance is running a long migration; raise {MigrationStartupOptions.LockTimeoutVariable} if it needs more time.",
                    ex);
            }

            _logger.LogInformation("[{InstanceId}] Acquired migration lock", instanceId);
        }

        private async Task ReleaseMigrationLockAsync(NpgsqlConnection connection, string instanceId)
        {
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockId)", connection);
                cmd.Parameters.AddWithValue("lockId", MIGRATION_LOCK_ID);
                await cmd.ExecuteScalarAsync();
                _logger.LogDebug("[{InstanceId}] Released migration lock", instanceId);
            }
            catch (Exception ex)
            {
                // The session lock is released when the connection closes anyway.
                _logger.LogWarning(ex, "[{InstanceId}] Failed to release migration lock explicitly", instanceId);
            }
        }

        /// <summary>
        /// Seed essential default data. Runs under the migration lock, so concurrent
        /// instances cannot double-seed. Non-fatal: a seed failure logs an error but
        /// does not block startup.
        /// </summary>
        private async Task SeedDefaultDataAsync(
            ConduitDbContext context,
            string instanceId,
            CancellationToken cancellationToken)
        {
            try
            {
                await SeedDefaultRetentionPolicyAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{InstanceId}] Error seeding default data - application will continue", instanceId);
            }
        }

        private async Task SeedDefaultRetentionPolicyAsync(ConduitDbContext context, CancellationToken cancellationToken)
        {
            // Check if any default policy exists
            var hasDefault = await context.MediaRetentionPolicies
                .AnyAsync(p => p.IsDefault, cancellationToken);

            if (hasDefault)
            {
                _logger.LogDebug("Default media retention policy already exists");
                return;
            }

            // Check if any policies exist at all
            var hasAnyPolicy = await context.MediaRetentionPolicies.AnyAsync(cancellationToken);

            if (hasAnyPolicy)
            {
                _logger.LogInformation(
                    "Media retention policies exist but none is marked as default. " +
                    "Consider setting a default policy via the Admin API.");
                return;
            }

            // Create default policy with reasonable settings
            var defaultPolicy = new MediaRetentionPolicy
            {
                Name = "Default",
                Description = "System default retention policy. Media retention varies by account balance: " +
                             "60 days for positive balance, 14 days for zero balance, 3 days for negative balance.",
                PositiveBalanceRetentionDays = 60,
                ZeroBalanceRetentionDays = 14,
                NegativeBalanceRetentionDays = 3,
                SoftDeleteGracePeriodDays = 7,
                RespectRecentAccess = true,
                RecentAccessWindowDays = 7,
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.MediaRetentionPolicies.Add(defaultPolicy);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created default media retention policy: {PolicyName} " +
                "(Positive: {PositiveDays}d, Zero: {ZeroDays}d, Negative: {NegativeDays}d)",
                defaultPolicy.Name,
                defaultPolicy.PositiveBalanceRetentionDays,
                defaultPolicy.ZeroBalanceRetentionDays,
                defaultPolicy.NegativeBalanceRetentionDays);
        }
    }
}
