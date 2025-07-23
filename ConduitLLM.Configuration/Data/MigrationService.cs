using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// A proper migration service that follows Entity Framework Core best practices
    /// </summary>
    public class MigrationService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _contextFactory;
        private readonly ILogger<MigrationService> _logger;
        private const long MIGRATION_LOCK_ID = 7891011;

        public MigrationService(
            IDbContextFactory<ConfigurationDbContext> contextFactory,
            ILogger<MigrationService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Apply migrations following EF Core best practices
        /// </summary>
        public async Task<MigrationResult> ApplyMigrationsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new MigrationResult();
            
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                
                // Step 1: Check database existence and connectivity
                var canConnect = await TestConnectionAsync(context, cancellationToken);
                if (!canConnect)
                {
                    result.Success = false;
                    result.Error = "Cannot connect to database";
                    return result;
                }

                // Step 2: Use transaction-scoped advisory lock for PostgreSQL
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                
                try
                {
                    // Acquire advisory lock within the transaction
                    var lockAcquired = await AcquireAdvisoryLockAsync(context, cancellationToken);
                    if (!lockAcquired)
                    {
                        _logger.LogInformation("Another instance is applying migrations. Waiting...");
                        
                        // Wait for other instance to complete
                        await transaction.RollbackAsync(cancellationToken);
                        await WaitForMigrationsAsync(context, TimeSpan.FromMinutes(5), cancellationToken);
                        
                        result.Success = true;
                        result.WasAppliedByOtherInstance = true;
                        return result;
                    }

                    // Step 3: Check migration state
                    var migrationState = await GetMigrationStateAsync(context, cancellationToken);
                    result.State = migrationState;

                    switch (migrationState)
                    {
                        case MigrationState.DatabaseDoesNotExist:
                            _logger.LogInformation("Database does not exist. Creating and applying all migrations...");
                            await context.Database.MigrateAsync(cancellationToken);
                            result.AppliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
                            break;

                        case MigrationState.NoMigrationsTable:
                            _logger.LogError("Database exists but no migrations table. This indicates EnsureCreated was used.");
                            result.Success = false;
                            result.Error = "Database was created with EnsureCreated. Manual intervention required.";
                            result.RequiresManualIntervention = true;
                            await transaction.RollbackAsync(cancellationToken);
                            return result;

                        case MigrationState.MigrationsPending:
                            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
                            _logger.LogInformation("Applying {Count} pending migrations", pending.Count());
                            await context.Database.MigrateAsync(cancellationToken);
                            result.AppliedMigrations = pending;
                            break;

                        case MigrationState.UpToDate:
                            _logger.LogInformation("Database is up to date");
                            break;

                        case MigrationState.HasUnappliedMigrationsInHistory:
                            _logger.LogError("Migration history contains migrations not in the codebase");
                            result.Success = false;
                            result.Error = "Database has migrations from a newer version of the application";
                            result.RequiresManualIntervention = true;
                            await transaction.RollbackAsync(cancellationToken);
                            return result;
                    }

                    await transaction.CommitAsync(cancellationToken);
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed");
                result.Success = false;
                result.Error = ex.Message;
                result.Exception = ex;
            }

            return result;
        }

        private async Task<bool> TestConnectionAsync(
            ConfigurationDbContext context, 
            CancellationToken cancellationToken)
        {
            try
            {
                await context.Database.OpenConnectionAsync(cancellationToken);
                await context.Database.CloseConnectionAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> AcquireAdvisoryLockAsync(
            ConfigurationDbContext context,
            CancellationToken cancellationToken)
        {
            var connection = context.Database.GetDbConnection() as NpgsqlConnection;
            if (connection == null)
                throw new InvalidOperationException("Not a PostgreSQL connection");

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_xact_lock($1)";
            cmd.Parameters.Add(new NpgsqlParameter { Value = MIGRATION_LOCK_ID });
            
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result is bool success && success;
        }

        private async Task<MigrationState> GetMigrationStateAsync(
            ConfigurationDbContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                // Check if database exists
                if (!await context.Database.CanConnectAsync(cancellationToken))
                    return MigrationState.DatabaseDoesNotExist;

                // Check for migrations table
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(cancellationToken);
                
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT EXISTS (
                        SELECT FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_name = '__EFMigrationsHistory'
                    )";
                
                var hasMigrationsTable = (bool)(await cmd.ExecuteScalarAsync(cancellationToken) ?? false);
                
                if (!hasMigrationsTable)
                {
                    // Check if any tables exist (indicates EnsureCreated was used)
                    cmd.CommandText = @"
                        SELECT EXISTS (
                            SELECT FROM information_schema.tables 
                            WHERE table_schema = 'public' 
                            AND table_name != '__EFMigrationsHistory'
                        )";
                    
                    var hasOtherTables = (bool)(await cmd.ExecuteScalarAsync(cancellationToken) ?? false);
                    return hasOtherTables ? MigrationState.NoMigrationsTable : MigrationState.DatabaseDoesNotExist;
                }

                // Check migration state
                var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
                var all = context.Database.GetMigrations();
                var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);

                if (pending.Any())
                    return MigrationState.MigrationsPending;

                if (applied.Except(all).Any())
                    return MigrationState.HasUnappliedMigrationsInHistory;

                return MigrationState.UpToDate;
            }
            finally
            {
                if (context.Database.GetDbConnection().State == ConnectionState.Open)
                    await context.Database.CloseConnectionAsync();
            }
        }

        private async Task WaitForMigrationsAsync(
            ConfigurationDbContext context,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            
            while (DateTime.UtcNow < deadline)
            {
                var state = await GetMigrationStateAsync(context, cancellationToken);
                if (state == MigrationState.UpToDate)
                    return;
                
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            
            throw new TimeoutException($"Timed out waiting for migrations after {timeout}");
        }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public Exception? Exception { get; set; }
        public MigrationState State { get; set; }
        public IEnumerable<string> AppliedMigrations { get; set; } = Enumerable.Empty<string>();
        public bool WasAppliedByOtherInstance { get; set; }
        public bool RequiresManualIntervention { get; set; }
    }

    public enum MigrationState
    {
        Unknown,
        DatabaseDoesNotExist,
        NoMigrationsTable,
        MigrationsPending,
        UpToDate,
        HasUnappliedMigrationsInHistory
    }
}