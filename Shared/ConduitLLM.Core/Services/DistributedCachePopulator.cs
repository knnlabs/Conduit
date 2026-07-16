using System.Collections.Concurrent;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implements cache stampede prevention using hybrid local + distributed locking.
    /// When multiple requests hit a cache miss simultaneously, only one performs the
    /// database query while others wait for the result.
    /// </summary>
    public class DistributedCachePopulator : IDistributedCachePopulator
    {
        private readonly IDistributedLockService _lockService;
        private readonly ILogger<DistributedCachePopulator> _logger;

        // Local locks prevent same-instance stampedes (faster than distributed locks)
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _localLocks = new();

        // Configuration
        private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

        public DistributedCachePopulator(
            IDistributedLockService lockService,
            ILogger<DistributedCachePopulator> logger)
        {
            _lockService = lockService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<T?> GetOrPopulateAsync<T>(
            string lockKey,
            Func<Task<T?>> cacheCheck,
            Func<Task<T?>> factory,
            CancellationToken cancellationToken = default) where T : class
        {
            // Step 1: Fast path - check cache without any locking
            try
            {
                var cachedValue = await cacheCheck();
                if (cachedValue != null)
                {
                    return cachedValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache check failed for key {LockKey}, proceeding to population", lockKey);
            }

            // Step 2: Acquire local lock to prevent same-instance stampede
            var localLock = _localLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            try
            {
                // Wait for local lock with timeout
                if (!await localLock.WaitAsync(LockTimeout, cancellationToken))
                {
                    _logger.LogWarning("Timeout waiting for local lock on {LockKey}, falling back to factory", lockKey);
                    return await factory();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error acquiring local lock for {LockKey}, falling back to factory", lockKey);
                return await factory();
            }

            try
            {
                // Step 3: Double-check cache after acquiring local lock
                try
                {
                    var cachedValue = await cacheCheck();
                    if (cachedValue != null)
                    {
                        _logger.LogDebug("Cache hit after local lock for {LockKey}", lockKey);
                        return cachedValue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cache double-check failed for {LockKey}", lockKey);
                }

                // Step 4: Acquire distributed lock to prevent cross-instance stampede
                IDistributedLock? distributedLock = null;
                try
                {
                    distributedLock = await _lockService.AcquireLockWithRetryAsync(
                        lockKey,
                        LockExpiry,
                        LockTimeout,
                        RetryDelay,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to acquire distributed lock for {LockKey}, proceeding without it", lockKey);
                    // Proceed without distributed lock - local lock still provides some protection
                }

                try
                {
                    // Step 5: Triple-check cache after acquiring distributed lock
                    // Another instance may have populated it while we were waiting
                    if (distributedLock != null)
                    {
                        try
                        {
                            var cachedValue = await cacheCheck();
                            if (cachedValue != null)
                            {
                                _logger.LogDebug("Cache hit after distributed lock for {LockKey}", lockKey);
                                return cachedValue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Cache triple-check failed for {LockKey}", lockKey);
                        }
                    }

                    // Step 6: Call factory (database fallback)
                    _logger.LogDebug("Executing factory for {LockKey}", lockKey);
                    return await factory();
                }
                finally
                {
                    // Step 7: Release distributed lock
                    if (distributedLock != null)
                    {
                        try
                        {
                            await distributedLock.ReleaseAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error releasing distributed lock for {LockKey}", lockKey);
                        }
                    }
                }
            }
            finally
            {
                // Release local lock
                localLock.Release();

                // Cleanup: Remove semaphore from dictionary if no one is waiting
                // This prevents memory leaks from accumulating semaphores
                if (localLock.CurrentCount == 1)
                {
                    _localLocks.TryRemove(lockKey, out _);
                }
            }
        }
    }
}
