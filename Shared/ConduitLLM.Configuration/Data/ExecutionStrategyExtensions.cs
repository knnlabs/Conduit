using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Transaction helpers compatible with EnableRetryOnFailure. A retrying execution
    /// strategy forbids user-initiated transactions outside of it
    /// (<c>BeginTransactionAsync</c> throws), so transactional units must run through
    /// the strategy with begin/commit inside — and be safe to re-run from the top,
    /// since a transient failure retries the whole delegate.
    /// </summary>
    public static class ExecutionStrategyExtensions
    {
        /// <summary>
        /// Execute <paramref name="work"/> inside a database transaction via the
        /// context's execution strategy. The delegate must be idempotent from the top:
        /// on transient failure the transaction rolls back and the whole delegate runs
        /// again.
        /// </summary>
        public static Task ExecuteInTransactionAsync(
            this DbContext context,
            Func<CancellationToken, Task> work,
            CancellationToken cancellationToken = default)
        {
            return context.ExecuteInTransactionAsync<object?>(
                async ct =>
                {
                    await work(ct);
                    return null;
                },
                cancellationToken);
        }

        /// <summary>
        /// Execute <paramref name="work"/> inside a database transaction via the
        /// context's execution strategy and return its result. The delegate must be
        /// idempotent from the top: on transient failure the transaction rolls back
        /// and the whole delegate runs again.
        /// </summary>
        public static Task<TResult> ExecuteInTransactionAsync<TResult>(
            this DbContext context,
            Func<CancellationToken, Task<TResult>> work,
            CancellationToken cancellationToken = default)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return strategy.ExecuteAsync(async ct =>
            {
                await using IDbContextTransaction transaction =
                    await context.Database.BeginTransactionAsync(ct);
                var result = await work(ct);
                await transaction.CommitAsync(ct);
                return result;
            }, cancellationToken);
        }
    }
}
