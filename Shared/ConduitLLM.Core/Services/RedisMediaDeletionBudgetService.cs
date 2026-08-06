using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of media deletion budget tracking.
    /// Uses monthly-keyed counters with automatic expiry.
    /// </summary>
    public class RedisMediaDeletionBudgetService : MediaDeletionBudgetServiceBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisMediaDeletionBudgetService> _logger;
        private readonly MediaBudgetFailureMode _failureMode;
        private DateTime? _lastFailureAtUtc;
        private const string BudgetKeyPrefix = "media:monthly-deletes:";
        private const string RESERVE_SCRIPT = """
            local current = tonumber(redis.call('GET', KEYS[1]) or '0')
            local remaining = tonumber(ARGV[2]) - current
            local requested = tonumber(ARGV[1])
            local granted = math.min(requested, math.max(remaining, 0))
            if granted > 0 then
                current = redis.call('INCRBY', KEYS[1], granted)
                if current == granted then
                    redis.call('EXPIRE', KEYS[1], 3024000)
                end
            end
            return {granted, current}
            """;

        protected override string KeyPrefix => BudgetKeyPrefix;
        public override string BackendName => "Redis";
        public override bool IsPersistent => true;
        public override DateTime? LastFailureAtUtc => _lastFailureAtUtc;

        public RedisMediaDeletionBudgetService(
            IConnectionMultiplexer redis,
            ILogger<RedisMediaDeletionBudgetService> logger,
            IOptions<MediaLifecycleOptions> options)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _failureMode = options.Value.BudgetFailureMode;
        }

        public RedisMediaDeletionBudgetService(
            IConnectionMultiplexer redis,
            ILogger<RedisMediaDeletionBudgetService> logger)
            : this(
                redis,
                logger,
                Microsoft.Extensions.Options.Options.Create(new MediaLifecycleOptions()))
        {
        }

        /// <inheritdoc/>
        public override async Task<long> GetMonthlyDeleteCountAsync(CancellationToken cancellationToken = default)
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
                RecordFailure(ex, "read");
                return _failureMode == MediaBudgetFailureMode.FailOpen
                    ? 0
                    : long.MaxValue;
            }
        }

        /// <inheritdoc/>
        public override async Task<long> IncrementMonthlyDeleteCountAsync(int count, CancellationToken cancellationToken = default)
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
                RecordFailure(ex, "increment");
                return -1;
            }
        }

        /// <inheritdoc/>
        public override async Task<MediaDeletionBudgetReservation> ReserveAsync(
            int requestedDeletions,
            int budget,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requested = Math.Max(0, requestedDeletions);
            if (requested == 0)
            {
                return new MediaDeletionBudgetReservation(
                    0,
                    0,
                    await GetMonthlyDeleteCountAsync(cancellationToken));
            }

            try
            {
                var db = _redis.GetDatabase();
                var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
                    RESERVE_SCRIPT,
                    new RedisKey[] { GetCurrentMonthKey() },
                    new RedisValue[] { requested, budget });
                if (result == null || result.Length != 2)
                {
                    throw new RedisException("Unexpected media budget reservation response");
                }

                return new MediaDeletionBudgetReservation(
                    requested,
                    checked((int)(long)result[0]),
                    (long)result[1]);
            }
            catch (Exception ex)
            {
                RecordFailure(ex, "reserve");
                var failOpen = _failureMode == MediaBudgetFailureMode.FailOpen;
                return new MediaDeletionBudgetReservation(
                    requested,
                    failOpen ? requested : 0,
                    -1,
                    StoreFailed: true,
                    FailureMode: _failureMode.ToString());
            }
        }

        private void RecordFailure(Exception exception, string operation)
        {
            _lastFailureAtUtc = DateTime.UtcNow;
            _logger.LogError(
                exception,
                "Media delete budget Redis {Operation} failed; applying {FailureMode}",
                operation,
                _failureMode);
            MediaDeletionBudgetMetrics.StoreFailures
                .WithLabels(BackendName, _failureMode.ToString(), operation)
                .Inc();
        }
    }
}
