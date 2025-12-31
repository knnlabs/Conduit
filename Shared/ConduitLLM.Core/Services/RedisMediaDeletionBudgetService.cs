using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of media deletion budget tracking.
    /// Uses monthly-keyed counters with automatic expiry.
    /// </summary>
    public class RedisMediaDeletionBudgetService : IMediaDeletionBudgetService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisMediaDeletionBudgetService> _logger;
        private const string KEY_PREFIX = "media:monthly-deletes:";

        public RedisMediaDeletionBudgetService(
            IConnectionMultiplexer redis,
            ILogger<RedisMediaDeletionBudgetService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<long> GetMonthlyDeleteCountAsync(CancellationToken cancellationToken = default)
        {
            var key = GetCurrentMonthKey();
            var db = _redis.GetDatabase();

            try
            {
                var value = await db.StringGetAsync(key);
                if (value.IsNullOrEmpty)
                {
                    return 0;
                }

                if (long.TryParse(value.ToString(), out var count))
                {
                    return count;
                }

                _logger.LogWarning("Invalid value in Redis for key {Key}: {Value}", key, value);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly delete count from Redis");
                // Return 0 on error to allow operations to proceed
                // Better to potentially exceed budget than to block all deletions
                return 0;
            }
        }

        /// <inheritdoc/>
        public async Task<long> IncrementMonthlyDeleteCountAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return await GetMonthlyDeleteCountAsync(cancellationToken);
            }

            var key = GetCurrentMonthKey();
            var db = _redis.GetDatabase();

            try
            {
                var newValue = await db.StringIncrementAsync(key, count);

                // Set expiry if this is a new key (first increment of the month)
                // Expire after 35 days to ensure it lasts the entire month plus buffer
                if (newValue == count)
                {
                    await db.KeyExpireAsync(key, TimeSpan.FromDays(35));
                    _logger.LogInformation(
                        "Created new monthly deletion counter for {Month} with initial count {Count}",
                        DateTime.UtcNow.ToString("yyyy-MM"), count);
                }

                _logger.LogDebug(
                    "Incremented monthly deletion counter by {Increment} to {Total}",
                    count, newValue);

                return newValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing monthly delete count in Redis");
                // Return -1 to indicate error, caller should handle appropriately
                return -1;
            }
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
            return $"{KEY_PREFIX}{DateTime.UtcNow:yyyy-MM}";
        }
    }
}
