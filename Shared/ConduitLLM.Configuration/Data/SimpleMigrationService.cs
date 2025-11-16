using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Dead simple migration service that just works.
    /// No EnsureCreated. No complex detection. Just migrations.
    /// </summary>
    public class SimpleMigrationService
    {
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;
        private readonly ILogger<SimpleMigrationService> _logger;
        
        // PostgreSQL advisory lock ID for migrations
        // This ensures only one instance runs migrations at a time
        private const long MIGRATION_LOCK_ID = 7891011;

        public SimpleMigrationService(
            IDbContextFactory<ConduitDbContext> contextFactory,
            ILogger<SimpleMigrationService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Apply migrations. That's it. That's all it does.
        /// </summary>
        public async Task<bool> MigrateAsync(CancellationToken cancellationToken = default)
        {
            var instanceId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{InstanceId}] Starting database migration", instanceId);

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                
                // Step 1: Try to acquire the migration lock
                var lockAcquired = await TryAcquireMigrationLockAsync(context, instanceId, cancellationToken);
                
                if (!lockAcquired)
                {
                    // Another instance is running migrations
                    _logger.LogInformation("[{InstanceId}] Another instance is running migrations. Waiting...", instanceId);
                    await WaitForMigrationsToCompleteAsync(context, instanceId, cancellationToken);
                    return true;
                }

                try
                {
                    // Step 2: We have the lock, run migrations
                    _logger.LogInformation("[{InstanceId}] Acquired migration lock. Running migrations...", instanceId);
                    
                    await context.Database.MigrateAsync(cancellationToken);
                    
                    _logger.LogInformation("[{InstanceId}] Migrations completed successfully", instanceId);
                    return true;
                }
                finally
                {
                    // Always release the lock
                    await ReleaseMigrationLockAsync(context, instanceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{InstanceId}] Migration failed", instanceId);
                
                // Check if this is a development environment and forced reset is enabled
                if (ShouldForceReset())
                {
                    _logger.LogWarning("[{InstanceId}] FORCE_RECREATE_DB_ON_FAILURE is enabled. Recreating database...", instanceId);
                    return await ForceRecreateDatabaseAsync(instanceId, cancellationToken);
                }
                
                throw;
            }
        }

        private async Task<bool> TryAcquireMigrationLockAsync(
            ConduitDbContext context, 
            string instanceId,
            CancellationToken cancellationToken)
        {
            try
            {
                var connection = context.Database.GetDbConnection() as NpgsqlConnection;
                if (connection == null)
                {
                    throw new InvalidOperationException("Database connection is not PostgreSQL");
                }

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var cmd = connection.CreateCommand();
                // pg_try_advisory_lock returns true if lock was acquired, false if already held
                cmd.CommandText = "SELECT pg_try_advisory_lock(@lockId)";
                cmd.Parameters.Add(new NpgsqlParameter("lockId", MIGRATION_LOCK_ID));

                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                var acquired = result is bool success && success;
                
                if (acquired)
                {
                    _logger.LogDebug("[{InstanceId}] Successfully acquired migration lock", instanceId);
                }
                else
                {
                    _logger.LogDebug("[{InstanceId}] Migration lock is held by another instance", instanceId);
                }
                
                return acquired;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{InstanceId}] Failed to acquire migration lock", instanceId);
                return false;
            }
        }

        private async Task ReleaseMigrationLockAsync(ConduitDbContext context, string instanceId)
        {
            try
            {
                var connection = context.Database.GetDbConnection() as NpgsqlConnection;
                if (connection?.State == System.Data.ConnectionState.Open)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT pg_advisory_unlock(@lockId)";
                    cmd.Parameters.Add(new NpgsqlParameter("lockId", MIGRATION_LOCK_ID));
                    
                    await cmd.ExecuteScalarAsync();
                    _logger.LogDebug("[{InstanceId}] Released migration lock", instanceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{InstanceId}] Failed to release migration lock", instanceId);
                // Lock will be released when connection closes anyway
            }
        }

        private async Task WaitForMigrationsToCompleteAsync(
            ConduitDbContext context,
            string instanceId,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 30; // Max 5 minutes with exponential backoff
            var random = new Random();
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Check if we can now acquire the lock (meaning other instance finished)
                var lockAcquired = await TryAcquireMigrationLockAsync(context, instanceId, cancellationToken);
                if (lockAcquired)
                {
                    // We got the lock, but migrations should already be done
                    // Release it immediately
                    await ReleaseMigrationLockAsync(context, instanceId);
                    _logger.LogInformation("[{InstanceId}] Migrations completed by another instance", instanceId);
                    return;
                }
                
                // Also check if database is accessible (migrations might be complete)
                try
                {
                    await context.Database.CanConnectAsync(cancellationToken);
                    
                    // Try to query a simple table to ensure schema exists
                    var connection = context.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync(cancellationToken);
                    
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT 1 FROM \"__EFMigrationsHistory\" LIMIT 1";
                    await cmd.ExecuteScalarAsync(cancellationToken);
                    
                    _logger.LogInformation("[{InstanceId}] Migrations completed by another instance", instanceId);
                    return; // Schema exists, migrations are complete
                }
                catch
                {
                    // Database not ready yet
                    _logger.LogDebug("[{InstanceId}] Waiting for migrations... Attempt {Attempt}/{MaxAttempts}", 
                        instanceId, attempt, maxAttempts);
                }
                
                if (attempt < maxAttempts)
                {
                    // Exponential backoff with jitter
                    var jitter = random.Next(0, 3000); // 0-3 seconds jitter
                    var delay = Math.Min(attempt * 2000, 10000) + jitter; // 2-10 seconds + jitter
                    await Task.Delay(delay, cancellationToken);
                }
            }
            
            throw new TimeoutException($"[{instanceId}] Timed out waiting for migrations to complete after {maxAttempts} attempts");
        }

        private bool ShouldForceReset()
        {
            var forceReset = Environment.GetEnvironmentVariable("FORCE_RECREATE_DB_ON_FAILURE")?.ToUpperInvariant() == "TRUE";
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToUpperInvariant() == "DEVELOPMENT";
            
            return forceReset && isDevelopment;
        }

        private async Task<bool> ForceRecreateDatabaseAsync(string instanceId, CancellationToken cancellationToken)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                
                _logger.LogWarning("[{InstanceId}] Dropping database...", instanceId);
                await context.Database.EnsureDeletedAsync(cancellationToken);
                
                _logger.LogWarning("[{InstanceId}] Applying migrations to fresh database...", instanceId);
                await context.Database.MigrateAsync(cancellationToken);
                
                _logger.LogWarning("[{InstanceId}] Database recreated successfully", instanceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{InstanceId}] Failed to recreate database", instanceId);
                return false;
            }
        }
    }
}