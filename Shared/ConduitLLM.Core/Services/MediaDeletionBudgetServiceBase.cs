using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Shares monthly-key and derived budget calculations across budget backends.
/// </summary>
public abstract class MediaDeletionBudgetServiceBase : IMediaDeletionBudgetService
{
    protected abstract string KeyPrefix { get; }

    public abstract string BackendName { get; }
    public abstract bool IsPersistent { get; }
    public abstract DateTime? LastFailureAtUtc { get; }

    public abstract Task<long> GetMonthlyDeleteCountAsync(
        CancellationToken cancellationToken = default);

    public abstract Task<long> IncrementMonthlyDeleteCountAsync(
        int count,
        CancellationToken cancellationToken = default);

    public abstract Task<MediaDeletionBudgetReservation> ReserveAsync(
        int requestedDeletions,
        int budget,
        CancellationToken cancellationToken = default);

    public async Task<bool> WouldExceedBudgetAsync(
        int proposedDeletions,
        int budget,
        CancellationToken cancellationToken = default)
    {
        var currentCount = await GetMonthlyDeleteCountAsync(cancellationToken);
        return currentCount + proposedDeletions > budget;
    }

    public async Task<long> GetRemainingBudgetAsync(
        int budget,
        CancellationToken cancellationToken = default)
    {
        var currentCount = await GetMonthlyDeleteCountAsync(cancellationToken);
        return Math.Max(0, budget - currentCount);
    }

    protected string GetCurrentMonthKey()
        => $"{KeyPrefix}{DateTime.UtcNow:yyyy-MM}";
}
