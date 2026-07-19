using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Exceptions;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Background service that batches Virtual Key spend updates to reduce database writes
    /// Provides events for cache invalidation integration
    /// </summary>
    public class BatchSpendUpdateService : BackgroundService, IBatchSpendUpdateService, IAsyncDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BatchSpendUpdateService> _logger;
        private readonly RedisConnectionFactory _redisConnectionFactory;
        private readonly IBillingAlertingService _alertingService;
        private readonly IRedisCircuitBreaker? _circuitBreaker;
        private readonly BatchSpendingOptions _options;
        private readonly Timer _flushTimer;
        private readonly TimeSpan _flushInterval;
        private readonly TimeSpan _redisTtl;
        private readonly string _redisKeyPrefix = "pending_spend:group:";
        private readonly string _processingKeyPrefix = "processing_spend:group:";
        private readonly string _keyUsagePrefix = "key_usage:group:";
        private readonly string _processingKeyUsagePrefix = "processing_key_usage:group:";
        private readonly ConcurrentQueue<(int VirtualKeyId, decimal Cost)> _fallbackQueue = new();

        /// <summary>
        /// Event raised after successful batch spend updates with the key hashes that were updated
        /// Allows external cache invalidation without tight coupling
        /// </summary>
        public event Action<string[]>? SpendUpdatesCompleted;

        /// <summary>
        /// Initializes a new instance of the BatchSpendUpdateService
        /// </summary>
        /// <param name="serviceScopeFactory">Service scope factory for creating scoped services</param>
        /// <param name="redisConnectionFactory">Redis connection factory</param>
        /// <param name="options">Batch spending configuration options</param>
        /// <param name="logger">Logger instance</param>
        public BatchSpendUpdateService(
            IServiceScopeFactory serviceScopeFactory,
            RedisConnectionFactory redisConnectionFactory,
            IOptions<BatchSpendingOptions> options,
            ILogger<BatchSpendUpdateService> logger,
            IBillingAlertingService alertingService,
            IRedisCircuitBreaker? circuitBreaker = null)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _redisConnectionFactory = redisConnectionFactory;
            _options = options.Value;
            _logger = logger;
            _alertingService = alertingService;
            _circuitBreaker = circuitBreaker;
            
            // Validate and apply configuration
            var validationResult = _options.Validate();
            if (validationResult != null)
            {
                _logger.LogError("Invalid BatchSpending configuration: {ValidationError}", validationResult.ErrorMessage);
                throw new InvalidOperationException($"Invalid BatchSpending configuration: {validationResult.ErrorMessage}");
            }
            
            _flushInterval = _options.GetValidatedFlushInterval();
            _redisTtl = _options.GetRedisTtl();
            
            _logger.LogInformation("BatchSpendUpdateService configured with flush interval: {FlushInterval}, Redis TTL: {RedisTtl}", 
                _flushInterval, _redisTtl);
            
            // Create timer for periodic flushing (in addition to background service)
            _flushTimer = new Timer(FlushPendingUpdatesCallback, null, _flushInterval, _flushInterval);
        }

        /// <summary>
        /// Gets whether the service is healthy and able to accept updates
        /// </summary>
        public bool IsHealthy =>
            (!_cancellationTokenSource?.Token.IsCancellationRequested ?? false) &&
            _circuitBreaker?.IsOpen != true;

        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Queues a spend update to Redis for batch processing.
        /// Throws on failure so the caller can fall back to alternative paths.
        /// </summary>
        /// <param name="virtualKeyId">Virtual Key ID to update</param>
        /// <param name="cost">Cost to add to the current spend</param>
        public async Task QueueSpendUpdateAsync(int virtualKeyId, decimal cost)
        {
            // Check circuit breaker if available
            if (_circuitBreaker?.IsOpen == true)
            {
                throw new RedisCircuitBreakerOpenException(
                    "Cannot update spend - Redis circuit breaker is open",
                    CircuitState.Open);
            }

            // Need to get the group ID for this key
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();

            var virtualKey = await context.VirtualKeys
                .Where(vk => vk.Id == virtualKeyId)
                .Select(vk => new { vk.VirtualKeyGroupId })
                .FirstOrDefaultAsync();

            if (virtualKey == null)
            {
                throw new BillingSystemException(
                    $"Virtual Key {virtualKeyId} was not found while queueing spend",
                    virtualKeyId,
                    BillingSystemException.ErrorCodes.DatabaseUpdateFailed);
            }

            // Execute Redis operations through circuit breaker if available
            if (_circuitBreaker != null)
            {
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    await PerformRedisUpdate(virtualKeyId, virtualKey.VirtualKeyGroupId, cost);
                });
            }
            else
            {
                await PerformRedisUpdate(virtualKeyId, virtualKey.VirtualKeyGroupId, cost);
            }

            _logger.LogDebug("Queued spend update to Redis for Virtual Key {VirtualKeyId} (Group {GroupId}): {Cost:C}",
                virtualKeyId, virtualKey.VirtualKeyGroupId, cost);
        }

        /// <inheritdoc />
        public void QueueFallbackUpdate(int virtualKeyId, decimal cost)
        {
            _fallbackQueue.Enqueue((virtualKeyId, cost));
            _logger.LogWarning(
                "Spend update for Virtual Key {VirtualKeyId} ({Cost:C}) queued to in-memory fallback. Will be flushed on next cycle.",
                virtualKeyId, cost);
        }

        private async Task PerformRedisUpdate(int virtualKeyId, int groupId, decimal cost)
        {
            var redis = await _redisConnectionFactory.GetConnectionAsync();
            var db = redis.GetDatabase();
            
            // Use group ID for accumulation
            var key = $"{_redisKeyPrefix}{groupId}";
            await db.StringIncrementAsync(key, (double)cost);
            
            // Also track which key was used (for transaction history)
            var keyUsageKey = $"key_usage:group:{groupId}:key:{virtualKeyId}";
            await db.StringIncrementAsync(keyUsageKey, (double)cost);
            
            // Set TTL for safety
            await db.KeyExpireAsync(key, _redisTtl);
            await db.KeyExpireAsync(keyUsageKey, _redisTtl);
        }

        /// <summary>
        /// Get the current pending spend for a Virtual Key
        /// </summary>
        /// <param name="virtualKeyId">Virtual Key ID</param>
        /// <returns>Pending spend amount</returns>
        public async Task<decimal> GetPendingSpendAsync(int virtualKeyId)
        {
            try
            {
                var redis = await _redisConnectionFactory.GetConnectionAsync();
                var db = redis.GetDatabase();
                
                var key = $"{_redisKeyPrefix}{virtualKeyId}";
                var value = await db.StringGetAsync(key);
                
                if (value.HasValue && double.TryParse(value.ToString(), out var pendingSpend))
                {
                    return (decimal)pendingSpend;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending spend from Redis for Virtual Key {VirtualKeyId}", virtualKeyId);
                return 0;
            }
        }

        /// <summary>
        /// Force flush all pending updates immediately
        /// </summary>
        /// <returns>Number of groups updated</returns>
        public async Task<int> FlushPendingUpdatesAsync()
        {
            try
            {
                var redis = await _redisConnectionFactory.GetConnectionAsync();
                var db = redis.GetDatabase();
                var server = redis.GetServer(redis.GetEndPoints()[0]);

                // Recover durable claims left by a database failure or process crash first,
                // then atomically move current pending amounts into new claims. New usage
                // can continue accumulating under the original pending keys while a claim
                // is written to PostgreSQL.
                var claims = await GetProcessingClaimsAsync(server, db);
                claims.AddRange(await ClaimPendingSpendAsync(server, db));

                if (claims.Count == 0)
                {
                    _logger.LogDebug("No pending spend updates to flush");
                    return 0;
                }

                _logger.LogDebug("Flushing {PendingCount} durable spend claims from Redis", claims.Count);

                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();
                var groupRepository = scope.ServiceProvider.GetRequiredService<IVirtualKeyGroupRepository>();

                // Process each group
                var updatedKeyHashes = new List<string>();
                var flushStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var processedCount = 0;
                decimal totalSpend = 0;

                foreach (var claim in claims)
                {
                    // Create a description that includes which keys were used
                    var description = BuildUsageDescription(claim.KeyUsageByKeyId);

                    // The claim ID is persisted on the ledger row in the same transaction as
                    // the debit. If the process dies after the DB commit but before deleting
                    // the Redis claim, recovery observes Applied=false and only acknowledges
                    // the already-recorded claim.
                    var result = await groupRepository.AdjustBalanceIdempotentAsync(
                        claim.GroupId,
                        -claim.TotalCost,
                        $"batch-spend:{claim.ClaimId}",
                        description,
                        "System",
                        ReferenceType.System,
                        claim.ClaimId);

                    // Acknowledge only after the database commit (or idempotent duplicate
                    // confirmation). Until this delete succeeds, the claim remains durable
                    // and retryable in Redis.
                    await DeleteClaimAsync(db, claim);

                    processedCount++;
                    totalSpend += result.Applied ? claim.TotalCost : 0;
                    _logger.LogDebug(
                        "Batch flush: finalized claim {ClaimId} for group {GroupId} — amount {Cost:C}, new balance: {NewBalance:C}, applied: {Applied} ({Processed}/{Total})",
                        claim.ClaimId, claim.GroupId, claim.TotalCost, result.NewBalance, result.Applied, processedCount, claims.Count);

                    // Note: We don't need to create additional transaction records here
                    // because AdjustBalanceIdempotentAsync already creates one with the correct balance.
                    // The individual key usage tracking is already handled in the description.

                    // Get keys in this group for cache invalidation
                    var groupKeys = await context.VirtualKeys
                        .AsNoTracking()
                        .Where(vk => vk.VirtualKeyGroupId == claim.GroupId)
                        .Select(vk => new { vk.Id, vk.KeyHash })
                        .ToListAsync();

                    updatedKeyHashes.AddRange(groupKeys.Select(k => k.KeyHash));
                }

                flushStopwatch.Stop();
                _logger.LogInformation(
                    "Batch flush completed: {ClaimCount} claims, total newly deducted: {TotalSpend:C}, affected keys: {KeyCount}, elapsed: {ElapsedMs}ms",
                    processedCount, totalSpend, updatedKeyHashes.Count, flushStopwatch.ElapsedMilliseconds);

                // Drain in-memory fallback queue
                var fallbackCount = 0;
                while (_fallbackQueue.TryDequeue(out var fallbackItem))
                {
                    try
                    {
                        var fallbackKey = await context.VirtualKeys
                            .Where(vk => vk.Id == fallbackItem.VirtualKeyId)
                            .Select(vk => new { vk.VirtualKeyGroupId, vk.KeyHash })
                            .FirstOrDefaultAsync();

                        if (fallbackKey != null)
                        {
                            await groupRepository.AdjustBalanceAsync(
                                fallbackKey.VirtualKeyGroupId,
                                -fallbackItem.Cost,
                                $"API usage by virtual key #{fallbackItem.VirtualKeyId} (recovered from fallback queue)",
                                "System");
                            updatedKeyHashes.Add(fallbackKey.KeyHash);
                            fallbackCount++;
                        }
                        else
                        {
                            _logger.LogWarning("Virtual Key {VirtualKeyId} from fallback queue not found — spend update lost",
                                fallbackItem.VirtualKeyId);
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx,
                            "Failed to process fallback spend update for Virtual Key {VirtualKeyId}. Re-queuing.",
                            fallbackItem.VirtualKeyId);
                        // Re-queue for the next flush cycle
                        _fallbackQueue.Enqueue(fallbackItem);
                        break; // Stop processing fallback queue on error to avoid infinite loop
                    }
                }

                if (fallbackCount > 0)
                {
                    _logger.LogInformation("Recovered {Count} spend updates from in-memory fallback queue", fallbackCount);
                }

                // Raise event for cache invalidation (if any subscribers)
                if (updatedKeyHashes.Any() && SpendUpdatesCompleted != null)
                {
                    try
                    {
                        SpendUpdatesCompleted.Invoke(updatedKeyHashes.ToArray());
                        _logger.LogDebug("Raised SpendUpdatesCompleted event for {Count} Virtual Keys", updatedKeyHashes.Count());
                    }
                    catch (Exception eventEx)
                    {
                        _logger.LogWarning(eventEx, "Error in SpendUpdatesCompleted event handler");
                        // Don't fail the operation if event handler fails
                    }
                }

                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch spend update");
                throw;
            }
        }

        /// <summary>
        /// Parses a group ID from a Redis key string by stripping the key prefix.
        /// </summary>
        /// <param name="keyString">The full Redis key string (e.g., "pending_spend:group:42")</param>
        /// <returns>The parsed group ID</returns>
        private int ParseGroupIdFromKey(string keyString)
        {
            return int.Parse(keyString.Substring(_redisKeyPrefix.Length));
        }

        private async Task<List<SpendClaim>> ClaimPendingSpendAsync(IServer server, IDatabase db)
        {
            var claims = new List<SpendClaim>();
            var pendingKeys = server.Keys(pattern: $"{_redisKeyPrefix}*").ToList();

            foreach (var pendingKey in pendingKeys)
            {
                var groupId = ParseGroupIdFromKey(pendingKey.ToString());
                var claimId = Guid.NewGuid().ToString("N");
                var processingKey = ProcessingSpendKey(groupId, claimId);

                bool claimed;
                try
                {
                    // RENAME is atomic in Redis. Once it completes, concurrent writers create
                    // a fresh pending key and cannot be erased when this claim is acknowledged.
                    claimed = await db.KeyRenameAsync(pendingKey, processingKey, When.NotExists);
                }
                catch (RedisServerException ex) when (ex.Message.Contains("no such key", StringComparison.OrdinalIgnoreCase))
                {
                    // Another flusher claimed the key after the server scan.
                    continue;
                }

                if (!claimed)
                {
                    continue;
                }

                await db.KeyExpireAsync(processingKey, _redisTtl);
                var usageKeys = await ClaimKeyUsageAsync(server, db, groupId, claimId);
                var claim = await ReadClaimAsync(db, processingKey, groupId, claimId, usageKeys);
                if (claim != null)
                {
                    claims.Add(claim);
                }
            }

            return claims;
        }

        private async Task<List<SpendClaim>> GetProcessingClaimsAsync(IServer server, IDatabase db)
        {
            var claims = new List<SpendClaim>();
            var processingKeys = server.Keys(pattern: $"{_processingKeyPrefix}*").ToList();

            foreach (var processingKey in processingKeys)
            {
                if (!TryParseProcessingSpendKey(processingKey.ToString(), out var groupId, out var claimId))
                {
                    _logger.LogWarning("Ignoring malformed batch spend claim key {ClaimKey}", processingKey);
                    continue;
                }

                var usageKeys = server.Keys(
                    pattern: $"{_processingKeyUsagePrefix}{groupId}:key:*:claim:{claimId}").ToList();
                var claim = await ReadClaimAsync(db, processingKey, groupId, claimId, usageKeys);
                if (claim != null)
                {
                    claims.Add(claim);
                }
            }

            return claims;
        }

        private async Task<List<RedisKey>> ClaimKeyUsageAsync(
            IServer server,
            IDatabase db,
            int groupId,
            string claimId)
        {
            var claimedKeys = new List<RedisKey>();
            var pendingUsageKeys = server.Keys(pattern: $"{_keyUsagePrefix}{groupId}:key:*").ToList();

            foreach (var pendingUsageKey in pendingUsageKeys)
            {
                if (!TryParsePendingKeyUsageKey(pendingUsageKey.ToString(), out var keyId))
                {
                    continue;
                }

                var processingUsageKey = ProcessingKeyUsageKey(groupId, keyId, claimId);
                bool claimed;
                try
                {
                    claimed = await db.KeyRenameAsync(pendingUsageKey, processingUsageKey, When.NotExists);
                }
                catch (RedisServerException ex) when (ex.Message.Contains("no such key", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (claimed)
                {
                    await db.KeyExpireAsync(processingUsageKey, _redisTtl);
                    claimedKeys.Add(processingUsageKey);
                }
            }

            return claimedKeys;
        }

        private async Task<SpendClaim?> ReadClaimAsync(
            IDatabase db,
            RedisKey processingKey,
            int groupId,
            string claimId,
            List<RedisKey> usageKeys)
        {
            var value = await db.StringGetAsync(processingKey);
            if (!TryParseRedisAmount(value, out var totalCost))
            {
                _logger.LogError("Batch spend claim {ClaimId} for group {GroupId} has an invalid amount", claimId, groupId);
                return null;
            }

            var keyUsageByKeyId = new Dictionary<int, decimal>();
            foreach (var usageKey in usageKeys)
            {
                if (!TryParseProcessingKeyUsageKey(usageKey.ToString(), out var keyId) ||
                    !TryParseRedisAmount(await db.StringGetAsync(usageKey), out var cost))
                {
                    continue;
                }

                keyUsageByKeyId[keyId] = cost;
            }

            return new SpendClaim(groupId, claimId, processingKey, totalCost, keyUsageByKeyId, usageKeys);
        }

        private static async Task DeleteClaimAsync(IDatabase db, SpendClaim claim)
        {
            var keys = claim.ProcessingKeyUsageKeys
                .Append(claim.ProcessingSpendKey)
                .ToArray();
            await db.KeyDeleteAsync(keys);
        }

        private RedisKey ProcessingSpendKey(int groupId, string claimId)
            => $"{_processingKeyPrefix}{groupId}:claim:{claimId}";

        private RedisKey ProcessingKeyUsageKey(int groupId, int keyId, string claimId)
            => $"{_processingKeyUsagePrefix}{groupId}:key:{keyId}:claim:{claimId}";

        private bool TryParseProcessingSpendKey(string key, out int groupId, out string claimId)
        {
            groupId = default;
            claimId = string.Empty;
            if (!key.StartsWith(_processingKeyPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = key[_processingKeyPrefix.Length..].Split(":claim:", 2, StringSplitOptions.None);
            return parts.Length == 2 &&
                   int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out groupId) &&
                   !string.IsNullOrWhiteSpace(claimId = parts[1]);
        }

        private bool TryParsePendingKeyUsageKey(string key, out int keyId)
        {
            keyId = default;
            var parts = key.Split(':');
            return parts.Length == 5 &&
                   int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out keyId);
        }

        private bool TryParseProcessingKeyUsageKey(string key, out int keyId)
        {
            keyId = default;
            var parts = key.Split(':');
            return parts.Length == 7 &&
                   int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out keyId);
        }

        private static bool TryParseRedisAmount(RedisValue value, out decimal amount)
        {
            amount = default;
            return value.HasValue &&
                   double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                   (amount = (decimal)parsed) >= 0;
        }

        private sealed record SpendClaim(
            int GroupId,
            string ClaimId,
            RedisKey ProcessingSpendKey,
            decimal TotalCost,
            Dictionary<int, decimal> KeyUsageByKeyId,
            List<RedisKey> ProcessingKeyUsageKeys);

        /// <summary>
        /// Builds a human-readable description of API usage for a given group,
        /// including which virtual keys contributed to the spend.
        /// </summary>
        /// <param name="keyUsageByKeyId">Dictionary of virtual key ID to cost for this claim.</param>
        /// <returns>A description string such as "API usage by virtual key #5"</returns>
        private static string BuildUsageDescription(Dictionary<int, decimal> keyUsageByKeyId)
        {
            if (keyUsageByKeyId.Count == 0)
            {
                return "API usage";
            }

            var keyIds = keyUsageByKeyId.Keys.ToList();
            if (keyIds.Count == 1)
            {
                return $"API usage by virtual key #{keyIds[0]}";
            }

            return $"API usage by {keyIds.Count} virtual keys";
        }

        /// <summary>
        /// Timer callback for periodic flushing
        /// </summary>
        private void FlushPendingUpdatesCallback(object? state)
        {
            // Fire and forget with proper error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await FlushPendingUpdatesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in periodic flush timer");
                }
            });
        }

        /// <summary>
        /// Background service execution
        /// </summary>
        /// <param name="stoppingToken">Cancellation token</param>
        /// <returns>Async task</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _logger.LogInformation("BatchSpendUpdateService started");

            // Check for any pending updates on startup
            try
            {
                var count = await FlushPendingUpdatesAsync();
                if (count > 0)
                {
                    _logger.LogInformation("Flushed {Count} pending updates from previous session", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing pending updates on startup");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Flush updates every interval
                    await Task.Delay(_flushInterval, stoppingToken);
                    await FlushPendingUpdatesAsync();
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in BatchSpendUpdateService background execution");
                    
                    // Continue running even if there's an error
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            // Final flush before stopping
            try
            {
                var finalCount = await FlushPendingUpdatesAsync();
                if (finalCount > 0)
                {
                    _logger.LogInformation("Flushed {Count} pending updates during shutdown", finalCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing pending updates during shutdown");
            }

            _logger.LogInformation("BatchSpendUpdateService stopped");
        }

        /// <summary>
        /// Async cleanup - preferred over Dispose() to avoid sync-over-async.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _flushTimer.DisposeAsync();

            try
            {
                await FlushPendingUpdatesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing pending updates during async disposal");
            }

            base.Dispose();
        }

        /// <summary>
        /// Sync cleanup fallback. Does not flush: pending updates are durable in Redis and
        /// are picked up by the next flush cycle, so blocking on async I/O here is unnecessary.
        /// </summary>
        public override void Dispose()
        {
            _flushTimer?.Dispose();
            base.Dispose();
        }

        /// <summary>
        /// Get statistics about the batching service
        /// </summary>
        /// <returns>Dictionary with service statistics</returns>
        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            try
            {
                var redis = await _redisConnectionFactory.GetConnectionAsync();
                var db = redis.GetDatabase();
                var server = redis.GetServer(redis.GetEndPoints()[0]);
                
                // Count pending keys
                var pattern = $"{_redisKeyPrefix}*";
                var keys = server.Keys(pattern: pattern).ToList();
                
                decimal totalPending = 0;
                foreach (var key in keys)
                {
                    var value = await db.StringGetAsync(key);
                    if (value.HasValue && double.TryParse(value.ToString(), out var cost))
                    {
                        totalPending += (decimal)cost;
                    }
                }
                
                return new Dictionary<string, object>
                {
                    ["PendingUpdates"] = keys.Count(),
                    ["TotalPendingCost"] = totalPending,
                    ["FlushIntervalSeconds"] = _flushInterval.TotalSeconds,
                    ["RedisTtlHours"] = _redisTtl.TotalHours,
                    ["ConfiguredFlushInterval"] = _options.FlushIntervalSeconds,
                    ["MinimumInterval"] = _options.MinimumIntervalSeconds,
                    ["MaximumInterval"] = _options.MaximumIntervalSeconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["FlushIntervalSeconds"] = _flushInterval.TotalSeconds,
                    ["ConfiguredFlushInterval"] = _options.FlushIntervalSeconds
                };
            }
        }
    }
}
