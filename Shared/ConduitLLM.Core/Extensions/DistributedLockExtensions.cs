using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Extensions;

public readonly record struct OptionalLockResult<T>(bool Executed, T? Value);

public static class DistributedLockExtensions
{
    /// <summary>
    /// Runs an operation with a distributed lock when one can be acquired and always
    /// releases an acquired lock. Acquisition failures can either skip the operation
    /// on timeout or run it without cross-instance coordination.
    /// </summary>
    public static async Task<OptionalLockResult<T>> RunWithOptionalLockAsync<T>(
        this IDistributedLockService? lockService,
        string lockKey,
        TimeSpan lockExpiry,
        TimeSpan lockTimeout,
        TimeSpan retryDelay,
        Func<bool, Task<T>> operation,
        ILogger logger,
        CancellationToken cancellationToken = default,
        bool skipOnTimeout = false)
    {
        IDistributedLock? distributedLock = null;
        if (lockService is not null)
        {
            try
            {
                distributedLock = await lockService.AcquireLockWithRetryAsync(
                    lockKey,
                    lockExpiry,
                    lockTimeout,
                    retryDelay,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException) when (skipOnTimeout)
            {
                return new OptionalLockResult<T>(false, default);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to acquire distributed lock {LockKey}; proceeding without coordination",
                    lockKey);
            }
        }
        else
        {
            logger.LogWarning(
                "Distributed lock service is unavailable for {LockKey}; proceeding without coordination",
                lockKey);
        }

        try
        {
            return new OptionalLockResult<T>(
                true,
                await operation(distributedLock is not null));
        }
        finally
        {
            if (distributedLock is not null)
            {
                try
                {
                    await distributedLock.ReleaseAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Error releasing distributed lock {LockKey}",
                        lockKey);
                }
            }
        }
    }
}
