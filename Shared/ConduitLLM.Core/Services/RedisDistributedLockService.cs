using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of distributed locking service.
    /// </summary>
    public class RedisDistributedLockService : IDistributedLockService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisDistributedLockService> _logger;

        public RedisDistributedLockService(
            IConnectionMultiplexer redis,
            ILogger<RedisDistributedLockService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<IDistributedLock?> AcquireLockAsync(
            string key,
            TimeSpan expiry,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Lock key cannot be null or empty", nameof(key));

            var lockKey = RedisKeys.Lock.For(key);
            var lockValue = Guid.NewGuid().ToString();
            var db = _redis.GetDatabase();

            // Use SET NX EX for atomic lock acquisition
            var acquired = await db.StringSetAsync(
                lockKey,
                lockValue,
                expiry,
                When.NotExists);

            if (acquired)
            {
                _logger.LogDebug("Acquired distributed lock for key {Key} with value {Value}", key, lockValue);
                return new RedisDistributedLock(db, lockKey, lockValue, DateTime.UtcNow.Add(expiry), _logger);
            }

            _logger.LogDebug("Failed to acquire distributed lock for key {Key} - lock already held", key);
            return null;
        }

        /// <inheritdoc/>

        /// <inheritdoc/>
        public async Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Lock key cannot be null or empty", nameof(key));

            var lockKey = RedisKeys.Lock.For(key);
            var db = _redis.GetDatabase();

            return await db.KeyExistsAsync(lockKey);
        }

        /// <inheritdoc/>
        public async Task<bool> ExtendLockAsync(IDistributedLock @lock, TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (@lock == null)
                throw new ArgumentNullException(nameof(@lock));

            var db = _redis.GetDatabase();
            var lockKey = @lock.Key;

            // Use Lua script to atomically check lock ownership and extend
            var script = @"
                local key = KEYS[1]
                local value = ARGV[1]
                local ttl = ARGV[2]

                if redis.call('GET', key) == value then
                    return redis.call('PEXPIRE', key, ttl)
                else
                    return 0
                end
            ";

            var result = await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { lockKey },
                new RedisValue[] { @lock.LockValue, (long)extension.TotalMilliseconds });

            var extended = (long)result == 1;

            if (extended)
            {
                _logger.LogDebug("Extended lock for key {Key} by {Extension}ms", @lock.Key, extension.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Failed to extend lock for key {Key} - lock not owned or expired", @lock.Key);
            }

            return extended;
        }

        /// <summary>
        /// Redis implementation of distributed lock.
        /// </summary>
        private class RedisDistributedLock : IDistributedLock
        {
            private readonly IDatabase _db;
            private readonly ILogger _logger;
            private bool _disposed;

            public string Key { get; }
            public string LockValue { get; }
            public DateTime ExpiryTime { get; private set; }
            public bool IsValid => !_disposed && DateTime.UtcNow < ExpiryTime;

            public RedisDistributedLock(
                IDatabase db,
                string key,
                string lockValue,
                DateTime expiryTime,
                ILogger logger)
            {
                _db = db ?? throw new ArgumentNullException(nameof(db));
                Key = key ?? throw new ArgumentNullException(nameof(key));
                LockValue = lockValue ?? throw new ArgumentNullException(nameof(lockValue));
                ExpiryTime = expiryTime;
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public async Task ReleaseAsync()
            {
                if (_disposed)
                    return;

                try
                {
                    // Use Lua script to ensure we only delete our own lock
                    var script = @"
                        local key = KEYS[1]
                        local value = ARGV[1]

                        if redis.call('GET', key) == value then
                            return redis.call('DEL', key)
                        else
                            return 0
                        end
                    ";

                    var result = await _db.ScriptEvaluateAsync(
                        script,
                        new RedisKey[] { Key },
                        new RedisValue[] { LockValue });

                    if ((long)result == 1)
                    {
                        _logger.LogDebug("Released distributed lock for key {Key}", Key);
                    }
                    else
                    {
                        _logger.LogWarning("Lock for key {Key} was not released - not owned or already expired", Key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing distributed lock for key {Key}", Key);
                }
                finally
                {
                    _disposed = true;
                }
            }

            public async ValueTask DisposeAsync()
            {
                await ReleaseAsync().ConfigureAwait(false);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    // Fire and forget release
                    _ = ReleaseAsync();
                }
            }
        }
    }
}