using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// PostgreSQL-based distributed lock service using advisory locks
    /// </summary>
    public class PostgresDistributedLockService : IDistributedLockService
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<PostgresDistributedLockService> _logger;
        private readonly ConcurrentDictionary<string, PostgresDistributedLock> _activeLocks;

        public PostgresDistributedLockService(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<PostgresDistributedLockService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeLocks = new ConcurrentDictionary<string, PostgresDistributedLock>();
        }

        /// <inheritdoc/>
        public async Task<IDistributedLock?> AcquireLockAsync(
            string key,
            TimeSpan expiry,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Lock key cannot be null or whitespace.", nameof(key));

            try
            {
                // Generate a hash code for the key to use as the advisory lock ID
                var lockId = GetLockId(key);
                
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var connection = context.Database.GetDbConnection() as NpgsqlConnection;
                
                if (connection == null)
                {
                    _logger.LogError("Unable to get NpgsqlConnection from DbContext");
                    return null;
                }

                // Ensure connection is open
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                // Try to acquire advisory lock (non-blocking)
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_try_advisory_lock(@lockId)";
                command.Parameters.AddWithValue("lockId", lockId);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                var acquired = result != null && (bool)result;

                if (acquired)
                {
                    var distributedLock = new PostgresDistributedLock(
                        key,
                        lockId.ToString(),
                        DateTime.UtcNow.Add(expiry),
                        connection,
                        _logger);

                    _activeLocks[key] = distributedLock;
                    _logger.LogDebug("Successfully acquired PostgreSQL advisory lock for key '{Key}' with ID {LockId}", key, lockId);
                    return distributedLock;
                }

                _logger.LogDebug("Failed to acquire PostgreSQL advisory lock for key '{Key}' - lock is already held", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring PostgreSQL advisory lock for key '{Key}'", key);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<IDistributedLock> AcquireLockWithRetryAsync(
            string key,
            TimeSpan expiry,
            TimeSpan timeout,
            TimeSpan retryDelay,
            CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var @lock = await AcquireLockAsync(key, expiry, cancellationToken);
                if (@lock != null)
                {
                    return @lock;
                }

                var remainingTime = endTime - DateTime.UtcNow;
                var delay = remainingTime < retryDelay ? remainingTime : retryDelay;
                
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw new TimeoutException($"Failed to acquire lock for key '{key}' within timeout period of {timeout}");
        }

        /// <inheritdoc/>
        public async Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_activeLocks.ContainsKey(key))
            {
                return true;
            }

            try
            {
                var lockId = GetLockId(key);
                
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                using var command = context.Database.GetDbConnection().CreateCommand();
                
                command.CommandText = @"
                    SELECT EXISTS (
                        SELECT 1 FROM pg_locks 
                        WHERE locktype = 'advisory' 
                        AND objid = @lockId
                    )";
                command.Parameters.Add(new NpgsqlParameter("lockId", lockId));

                await context.Database.OpenConnectionAsync(cancellationToken);
                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result != null && (bool)result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking lock status for key '{Key}'", key);
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<bool> ExtendLockAsync(IDistributedLock @lock, TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (@lock is PostgresDistributedLock pgLock)
            {
                pgLock.ExtendExpiry(extension);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// Generates a stable lock ID from a string key
        /// </summary>
        private static long GetLockId(string key)
        {
            // Use a stable hash function to generate consistent lock IDs
            // PostgreSQL advisory locks use bigint (64-bit)
            unchecked
            {
                long hash = 17;
                foreach (char c in key)
                {
                    hash = hash * 31 + c;
                }
                // Ensure positive value for advisory lock
                return Math.Abs(hash);
            }
        }

        /// <summary>
        /// Internal implementation of PostgreSQL distributed lock
        /// </summary>
        private class PostgresDistributedLock : IDistributedLock
        {
            private readonly NpgsqlConnection _connection;
            private readonly ILogger _logger;
            private readonly long _lockId;
            private DateTime _expiryTime;
            private bool _disposed;

            public PostgresDistributedLock(
                string key,
                string lockValue,
                DateTime expiryTime,
                NpgsqlConnection connection,
                ILogger logger)
            {
                Key = key;
                LockValue = lockValue;
                _expiryTime = expiryTime;
                _connection = connection;
                _logger = logger;
                _lockId = GetLockId(key);
            }

            public string Key { get; }
            public string LockValue { get; }
            public DateTime ExpiryTime => _expiryTime;
            public bool IsValid => !_disposed && DateTime.UtcNow < _expiryTime;

            public void ExtendExpiry(TimeSpan extension)
            {
                _expiryTime = _expiryTime.Add(extension);
            }

            public async Task ReleaseAsync()
            {
                if (_disposed)
                    return;

                try
                {
                    if (_connection.State == System.Data.ConnectionState.Open)
                    {
                        using var command = _connection.CreateCommand();
                        command.CommandText = "SELECT pg_advisory_unlock(@lockId)";
                        command.Parameters.AddWithValue("lockId", _lockId);
                        
                        var result = await command.ExecuteScalarAsync();
                        var released = result != null && (bool)result;
                        
                        if (released)
                        {
                            _logger.LogDebug("Successfully released PostgreSQL advisory lock for key '{Key}'", Key);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to release PostgreSQL advisory lock for key '{Key}' - lock may have already been released", Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing PostgreSQL advisory lock for key '{Key}'", Key);
                }
                finally
                {
                    _disposed = true;
                    _connection?.Dispose();
                }
            }

            public void Dispose()
            {
                ReleaseAsync().GetAwaiter().GetResult();
            }
        }
    }
}