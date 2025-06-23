using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// In-memory implementation of distributed locking service for development and testing.
    /// WARNING: This implementation only works within a single process instance.
    /// </summary>
    public class InMemoryDistributedLockService : IDistributedLockService
    {
        private readonly ConcurrentDictionary<string, LockInfo> _locks = new();
        private readonly ILogger<InMemoryDistributedLockService> _logger;
        private readonly Timer _cleanupTimer;

        public InMemoryDistributedLockService(ILogger<InMemoryDistributedLockService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Start cleanup timer to remove expired locks
            _cleanupTimer = new Timer(
                CleanupExpiredLocks, 
                null, 
                TimeSpan.FromMinutes(1), 
                TimeSpan.FromMinutes(1));
        }

        /// <inheritdoc/>
        public Task<IDistributedLock?> AcquireLockAsync(
            string key, 
            TimeSpan expiry, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Lock key cannot be null or empty", nameof(key));

            var lockValue = Guid.NewGuid().ToString();
            var expiryTime = DateTime.UtcNow.Add(expiry);
            var lockInfo = new LockInfo(lockValue, expiryTime);

            // Try to add the lock
            if (_locks.TryAdd(key, lockInfo))
            {
                _logger.LogDebug("Acquired in-memory lock for key {Key}", key);
                return Task.FromResult<IDistributedLock?>(
                    new InMemoryDistributedLock(this, key, lockValue, expiryTime, _logger));
            }

            // Check if existing lock is expired
            if (_locks.TryGetValue(key, out var existingLock) && existingLock.IsExpired)
            {
                // Try to update with new lock
                if (_locks.TryUpdate(key, lockInfo, existingLock))
                {
                    _logger.LogDebug("Acquired in-memory lock for key {Key} (previous lock expired)", key);
                    return Task.FromResult<IDistributedLock?>(
                        new InMemoryDistributedLock(this, key, lockValue, expiryTime, _logger));
                }
            }

            _logger.LogDebug("Failed to acquire in-memory lock for key {Key} - lock already held", key);
            return Task.FromResult<IDistributedLock?>(null);
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
                var lockHandle = await AcquireLockAsync(key, expiry, cancellationToken);
                if (lockHandle != null)
                {
                    return lockHandle;
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
        public Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Lock key cannot be null or empty", nameof(key));

            if (_locks.TryGetValue(key, out var lockInfo))
            {
                return Task.FromResult(!lockInfo.IsExpired);
            }

            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> ExtendLockAsync(IDistributedLock @lock, TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (@lock == null)
                throw new ArgumentNullException(nameof(@lock));

            if (_locks.TryGetValue(@lock.Key, out var lockInfo))
            {
                if (lockInfo.LockValue == @lock.LockValue && !lockInfo.IsExpired)
                {
                    var newExpiryTime = DateTime.UtcNow.Add(extension);
                    var newLockInfo = new LockInfo(@lock.LockValue, newExpiryTime);
                    
                    if (_locks.TryUpdate(@lock.Key, newLockInfo, lockInfo))
                    {
                        _logger.LogDebug("Extended in-memory lock for key {Key} by {Extension}ms", 
                            @lock.Key, extension.TotalMilliseconds);
                        return Task.FromResult(true);
                    }
                }
            }

            _logger.LogWarning("Failed to extend in-memory lock for key {Key} - lock not owned or expired", @lock.Key);
            return Task.FromResult(false);
        }

        internal bool ReleaseLock(string key, string lockValue)
        {
            if (_locks.TryGetValue(key, out var lockInfo))
            {
                if (lockInfo.LockValue == lockValue)
                {
                    if (_locks.TryRemove(key, out _))
                    {
                        _logger.LogDebug("Released in-memory lock for key {Key}", key);
                        return true;
                    }
                }
            }

            return false;
        }

        private void CleanupExpiredLocks(object? state)
        {
            try
            {
                var expiredKeys = _locks
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (_locks.TryRemove(key, out _))
                    {
                        _logger.LogDebug("Cleaned up expired in-memory lock for key {Key}", key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during in-memory lock cleanup");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _locks.Clear();
        }

        private class LockInfo
        {
            public string LockValue { get; }
            public DateTime ExpiryTime { get; }
            public bool IsExpired => DateTime.UtcNow >= ExpiryTime;

            public LockInfo(string lockValue, DateTime expiryTime)
            {
                LockValue = lockValue;
                ExpiryTime = expiryTime;
            }
        }

        private class InMemoryDistributedLock : IDistributedLock
        {
            private readonly InMemoryDistributedLockService _service;
            private readonly ILogger _logger;
            private bool _disposed;

            public string Key { get; }
            public string LockValue { get; }
            public DateTime ExpiryTime { get; }
            public bool IsValid => !_disposed && DateTime.UtcNow < ExpiryTime;

            public InMemoryDistributedLock(
                InMemoryDistributedLockService service,
                string key,
                string lockValue,
                DateTime expiryTime,
                ILogger logger)
            {
                _service = service ?? throw new ArgumentNullException(nameof(service));
                Key = key ?? throw new ArgumentNullException(nameof(key));
                LockValue = lockValue ?? throw new ArgumentNullException(nameof(lockValue));
                ExpiryTime = expiryTime;
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public Task ReleaseAsync()
            {
                if (_disposed)
                    return Task.CompletedTask;

                try
                {
                    _service.ReleaseLock(Key, LockValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing in-memory lock for key {Key}", Key);
                }
                finally
                {
                    _disposed = true;
                }

                return Task.CompletedTask;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _ = ReleaseAsync();
                }
            }
        }
    }
}