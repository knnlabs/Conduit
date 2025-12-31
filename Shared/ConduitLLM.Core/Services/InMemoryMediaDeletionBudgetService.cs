using System.Collections.Concurrent;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// In-memory implementation of media deletion budget tracking for development/testing.
    /// Note: Counters are not persisted and will reset on application restart.
    /// Note: Counters are not shared across multiple instances.
    /// </summary>
    public class InMemoryMediaDeletionBudgetService : IMediaDeletionBudgetService
    {
        private readonly ConcurrentDictionary<string, long> _monthlyCounts = new();
        private readonly ILogger<InMemoryMediaDeletionBudgetService> _logger;

        public InMemoryMediaDeletionBudgetService(ILogger<InMemoryMediaDeletionBudgetService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogWarning(
                "Using in-memory media deletion budget tracking. " +
                "Counters will not persist across restarts or be shared across instances.");
        }

        /// <inheritdoc/>
        public Task<long> GetMonthlyDeleteCountAsync(CancellationToken cancellationToken = default)
        {
            var key = GetCurrentMonthKey();
            var count = _monthlyCounts.GetValueOrDefault(key, 0);
            return Task.FromResult(count);
        }

        /// <inheritdoc/>
        public Task<long> IncrementMonthlyDeleteCountAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return GetMonthlyDeleteCountAsync(cancellationToken);
            }

            var key = GetCurrentMonthKey();
            var newValue = _monthlyCounts.AddOrUpdate(key, count, (_, existing) => existing + count);

            _logger.LogDebug(
                "Incremented in-memory monthly deletion counter by {Increment} to {Total}",
                count, newValue);

            // Clean up old months (keep only current and previous month)
            CleanupOldMonths();

            return Task.FromResult(newValue);
        }

        /// <inheritdoc/>
        public async Task<bool> WouldExceedBudgetAsync(int proposedDeletions, int budget, CancellationToken cancellationToken = default)
        {
            var currentCount = await GetMonthlyDeleteCountAsync(cancellationToken);
            return (currentCount + proposedDeletions) > budget;
        }

        /// <inheritdoc/>
        public async Task<long> GetRemainingBudgetAsync(int budget, CancellationToken cancellationToken = default)
        {
            var currentCount = await GetMonthlyDeleteCountAsync(cancellationToken);
            var remaining = budget - currentCount;
            return remaining > 0 ? remaining : 0;
        }

        private static string GetCurrentMonthKey()
        {
            return DateTime.UtcNow.ToString("yyyy-MM");
        }

        private void CleanupOldMonths()
        {
            var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var previousMonth = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM");

            foreach (var key in _monthlyCounts.Keys.ToList())
            {
                if (key != currentMonth && key != previousMonth)
                {
                    _monthlyCounts.TryRemove(key, out _);
                }
            }
        }
    }
}
