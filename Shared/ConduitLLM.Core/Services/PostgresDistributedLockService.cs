using System.Data;
using System.Text;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ConduitLLM.Core.Services;

/// <summary>
/// PostgreSQL distributed locks backed by session-scoped advisory locks.
/// Each returned handle owns the dedicated PostgreSQL connection that holds its lock.
/// </summary>
public class PostgresDistributedLockService : IDistributedLockService
{
    private const int DefaultKeepAliveSeconds = 30;

    private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
    private readonly ILogger<PostgresDistributedLockService> _logger;

    public PostgresDistributedLockService(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<PostgresDistributedLockService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IDistributedLock?> AcquireLockAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Lock key cannot be null or whitespace.", nameof(key));
        }

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "Lock expiry must be positive.");
        }

        NpgsqlConnection? connection = null;
        try
        {
            connection = await OpenDedicatedConnectionAsync(cancellationToken);
            var lockId = GetLockId(key);
            var acquired = await TryAcquireAsync(connection, lockId, cancellationToken);
            if (!acquired)
            {
                await connection.DisposeAsync();
                _logger.LogDebug(
                    "Failed to acquire PostgreSQL advisory lock for key '{Key}' because it is already held",
                    key);
                return null;
            }

            var distributedLock = new PostgresDistributedLock(
                key,
                lockId,
                expiry,
                connection,
                _logger);
            connection = null; // Ownership transferred to the handle.

            _logger.LogDebug(
                "Acquired PostgreSQL advisory lock for key '{Key}' with ID {LockId}",
                key, lockId);
            return distributedLock;
        }
        catch (OperationCanceledException)
        {
            if (connection != null)
            {
                await connection.DisposeAsync();
            }

            throw;
        }
        catch (Exception ex)
        {
            if (connection != null)
            {
                await connection.DisposeAsync();
            }

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
            var distributedLock = await AcquireLockAsync(key, expiry, cancellationToken);
            if (distributedLock != null)
            {
                return distributedLock;
            }

            var remainingTime = endTime - DateTime.UtcNow;
            var delay = remainingTime < retryDelay ? remainingTime : retryDelay;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(
            $"Failed to acquire lock for key '{key}' within timeout period of {timeout}");
    }

    /// <inheritdoc/>
    public async Task<bool> IsLockedAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Lock key cannot be null or whitespace.", nameof(key));
        }

        try
        {
            await using var connection = await OpenDedicatedConnectionAsync(cancellationToken);
            var lockId = GetLockId(key);

            // The most reliable status check is to attempt acquisition on another session.
            // If it succeeds, release it immediately; otherwise another session holds it.
            if (!await TryAcquireAsync(connection, lockId, cancellationToken))
            {
                return true;
            }

            await UnlockAsync(connection, lockId, cancellationToken);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking lock status for key '{Key}'", key);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> ExtendLockAsync(
        IDistributedLock distributedLock,
        TimeSpan extension,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (distributedLock is not PostgresDistributedLock postgresLock ||
            extension <= TimeSpan.Zero)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(postgresLock.TryExtend(extension));
    }

    /// <summary>
    /// Generates a stable FNV-1a 64-bit advisory-lock ID from a UTF-8 key.
    /// </summary>
    internal static long GetLockId(string key)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(key))
        {
            hash ^= value;
            hash *= prime;
        }

        return unchecked((long)hash);
    }

    private async Task<NpgsqlConnection> OpenDedicatedConnectionAsync(
        CancellationToken cancellationToken)
    {
        string connectionString;
        await using (var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var dbConnection = context.Database.GetDbConnection();
            if (dbConnection is not NpgsqlConnection)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL advisory locks require Npgsql; resolved {dbConnection.GetType().Name}.");
            }

            connectionString = dbConnection.ConnectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.KeepAlive == 0)
        {
            builder.KeepAlive = DefaultKeepAliveSeconds;
        }

        var connection = new NpgsqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> TryAcquireAsync(
        NpgsqlConnection connection,
        long lockId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(@lockId)",
            connection);
        command.Parameters.AddWithValue("lockId", lockId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> UnlockAsync(
        NpgsqlConnection connection,
        long lockId,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_unlock(@lockId)",
            connection);
        command.Parameters.AddWithValue("lockId", lockId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private sealed class PostgresDistributedLock : IDistributedLock
    {
        private readonly NpgsqlConnection _connection;
        private readonly ILogger _logger;
        private readonly long _lockId;
        private readonly Timer _expiryTimer;
        private readonly object _expiryGate = new();
        private DateTime _expiryTime;
        private int _releaseStarted;

        public PostgresDistributedLock(
            string key,
            long lockId,
            TimeSpan expiry,
            NpgsqlConnection connection,
            ILogger logger)
        {
            Key = key;
            LockValue = lockId.ToString();
            _lockId = lockId;
            _expiryTime = DateTime.UtcNow.Add(expiry);
            _connection = connection;
            _logger = logger;
            _expiryTimer = new Timer(
                static state => _ = ((PostgresDistributedLock)state!).ReleaseExpiredAsync(),
                this,
                expiry,
                Timeout.InfiniteTimeSpan);
        }

        public string Key { get; }
        public string LockValue { get; }

        public DateTime ExpiryTime
        {
            get
            {
                lock (_expiryGate)
                {
                    return _expiryTime;
                }
            }
        }

        public bool IsValid =>
            Volatile.Read(ref _releaseStarted) == 0 &&
            _connection.State == ConnectionState.Open &&
            DateTime.UtcNow < ExpiryTime;

        public bool TryExtend(TimeSpan extension)
        {
            lock (_expiryGate)
            {
                if (Volatile.Read(ref _releaseStarted) != 0 ||
                    _connection.State != ConnectionState.Open ||
                    DateTime.UtcNow >= _expiryTime)
                {
                    return false;
                }

                _expiryTime = _expiryTime.Add(extension);
                var dueTime = _expiryTime - DateTime.UtcNow;
                _expiryTimer.Change(
                    dueTime > TimeSpan.Zero ? dueTime : TimeSpan.Zero,
                    Timeout.InfiniteTimeSpan);
                return true;
            }
        }

        public async Task ReleaseAsync()
        {
            if (Interlocked.Exchange(ref _releaseStarted, 1) != 0)
            {
                return;
            }

            _expiryTimer.Dispose();
            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    var released = await UnlockAsync(_connection, _lockId);
                    if (released)
                    {
                        _logger.LogDebug(
                            "Released PostgreSQL advisory lock for key '{Key}'",
                            Key);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "PostgreSQL advisory lock for key '{Key}' was no longer held by its connection",
                            Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error explicitly releasing PostgreSQL advisory lock for key '{Key}'; closing its session",
                    Key);
            }
            finally
            {
                try
                {
                    await _connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error closing PostgreSQL advisory-lock session for key '{Key}'",
                        Key);
                }
            }
        }

        public async ValueTask DisposeAsync() =>
            await ReleaseAsync().ConfigureAwait(false);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _releaseStarted, 1) != 0)
            {
                return;
            }

            _expiryTimer.Dispose();
            try
            {
                _connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error closing PostgreSQL advisory-lock session for key '{Key}'",
                    Key);
            }
            _logger.LogDebug(
                "Closed PostgreSQL advisory-lock session for key '{Key}'",
                Key);
        }

        private async Task ReleaseExpiredAsync()
        {
            _logger.LogWarning(
                "PostgreSQL advisory lock for key '{Key}' reached its expiry and will be released",
                Key);
            await ReleaseAsync();
        }
    }
}
