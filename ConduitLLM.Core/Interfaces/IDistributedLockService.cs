namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Provides distributed locking capabilities for coordinating work across multiple service instances.
    /// </summary>
    public interface IDistributedLockService
    {
        /// <summary>
        /// Attempts to acquire a distributed lock for the specified key.
        /// </summary>
        /// <param name="key">The unique key identifying the resource to lock.</param>
        /// <param name="expiry">The duration after which the lock should automatically expire.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A disposable lock handle if acquired, null if the lock is already held.</returns>
        Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to acquire a distributed lock with retry logic.
        /// </summary>
        /// <param name="key">The unique key identifying the resource to lock.</param>
        /// <param name="expiry">The duration after which the lock should automatically expire.</param>
        /// <param name="timeout">The maximum time to wait for acquiring the lock.</param>
        /// <param name="retryDelay">The delay between retry attempts.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A disposable lock handle if acquired within the timeout period.</returns>
        Task<IDistributedLock> AcquireLockWithRetryAsync(
            string key, 
            TimeSpan expiry, 
            TimeSpan timeout,
            TimeSpan retryDelay,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a lock is currently held for the specified key.
        /// </summary>
        /// <param name="key">The unique key identifying the resource.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the lock is held, false otherwise.</returns>
        Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extends the expiry time of an existing lock.
        /// </summary>
        /// <param name="lock">The lock to extend.</param>
        /// <param name="extension">The additional time to extend the lock.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the lock was successfully extended.</returns>
        Task<bool> ExtendLockAsync(IDistributedLock @lock, TimeSpan extension, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a distributed lock that can be released when disposed.
    /// </summary>
    public interface IDistributedLock : IDisposable
    {
        /// <summary>
        /// Gets the unique key for this lock.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the unique value identifying this lock instance.
        /// </summary>
        string LockValue { get; }

        /// <summary>
        /// Gets when this lock will expire.
        /// </summary>
        DateTime ExpiryTime { get; }

        /// <summary>
        /// Gets whether this lock is still valid (not expired).
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Releases the lock asynchronously.
        /// </summary>
        Task ReleaseAsync();
    }
}