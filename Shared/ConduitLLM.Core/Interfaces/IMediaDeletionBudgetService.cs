namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Tracks monthly media deletion operations to stay within storage provider limits (e.g., R2 free tier).
    /// </summary>
    public interface IMediaDeletionBudgetService
    {
        /// <summary>
        /// Gets the current month's deletion count.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of delete operations performed this month</returns>
        Task<long> GetMonthlyDeleteCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Increments the monthly deletion counter.
        /// </summary>
        /// <param name="count">Number of deletions to add</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The new total count after incrementing</returns>
        Task<long> IncrementMonthlyDeleteCountAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if performing the specified number of deletions would exceed the monthly budget.
        /// </summary>
        /// <param name="proposedDeletions">Number of deletions being proposed</param>
        /// <param name="budget">Monthly budget limit</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if budget would be exceeded, false otherwise</returns>
        Task<bool> WouldExceedBudgetAsync(int proposedDeletions, int budget, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the remaining budget for the current month.
        /// </summary>
        /// <param name="budget">Monthly budget limit</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of deletions remaining in budget</returns>
        Task<long> GetRemainingBudgetAsync(int budget, CancellationToken cancellationToken = default);
    }
}
