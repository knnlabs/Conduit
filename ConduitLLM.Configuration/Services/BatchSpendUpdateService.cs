using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Background service that batches Virtual Key spend updates to reduce database writes
    /// Provides events for cache invalidation integration
    /// </summary>
    public class BatchSpendUpdateService : BackgroundService, IBatchSpendUpdateService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BatchSpendUpdateService> _logger;
        private readonly RedisConnectionFactory _redisConnectionFactory;
        private readonly BatchSpendingOptions _options;
        private readonly Timer _flushTimer;
        private readonly TimeSpan _flushInterval;
        private readonly TimeSpan _redisTtl;
        private readonly string _redisKeyPrefix = "pending_spend:group:";
        
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
            ILogger<BatchSpendUpdateService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _redisConnectionFactory = redisConnectionFactory;
            _options = options.Value;
            _logger = logger;
            
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
        public bool IsHealthy => !_cancellationTokenSource?.Token.IsCancellationRequested ?? false;

        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Add a spend update to the batch queue
        /// </summary>
        /// <param name="virtualKeyId">Virtual Key ID to update</param>
        /// <param name="cost">Cost to add to the current spend</param>
        public void QueueSpendUpdate(int virtualKeyId, decimal cost)
        {
            // Fire and forget pattern for non-blocking updates
            _ = Task.Run(async () =>
            {
                try
                {
                    // Need to get the group ID for this key
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();
                    
                    var virtualKey = await context.VirtualKeys
                        .Where(vk => vk.Id == virtualKeyId)
                        .Select(vk => new { vk.VirtualKeyGroupId })
                        .FirstOrDefaultAsync();
                    
                    if (virtualKey == null)
                    {
                        _logger.LogWarning("Virtual Key {VirtualKeyId} not found for spend update", virtualKeyId);
                        return;
                    }
                    
                    var redis = await _redisConnectionFactory.GetConnectionAsync();
                    var db = redis.GetDatabase();
                    
                    // Use group ID for accumulation
                    var key = $"{_redisKeyPrefix}{virtualKey.VirtualKeyGroupId}";
                    await db.StringIncrementAsync(key, (double)cost);
                    
                    // Also track which key was used (for transaction history)
                    var keyUsageKey = $"key_usage:group:{virtualKey.VirtualKeyGroupId}:key:{virtualKeyId}";
                    await db.StringIncrementAsync(keyUsageKey, (double)cost);
                    
                    // Set TTL for safety
                    await db.KeyExpireAsync(key, _redisTtl);
                    await db.KeyExpireAsync(keyUsageKey, _redisTtl);
                    
                    _logger.LogDebug("Queued spend update to Redis for Virtual Key {VirtualKeyId} (Group {GroupId}): {Cost:C}", 
                        virtualKeyId, virtualKey.VirtualKeyGroupId, cost);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to queue spend update to Redis for Virtual Key {VirtualKeyId}", virtualKeyId);
                    // In production, you might want to fall back to direct DB update here
                }
            });
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
                
                if (value.HasValue && double.TryParse(value, out var pendingSpend))
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
                
                // Get all pending spend keys for groups
                var pattern = $"{_redisKeyPrefix}*";
                var keys = server.Keys(pattern: pattern).ToList();
                
                if (keys.Count() == 0)
                {
                    return 0;
                }
                
                // Get and delete all values atomically
                var groupUpdates = new Dictionary<int, decimal>();
                var keyUsagePattern = "key_usage:group:*";
                var keyUsageKeys = server.Keys(pattern: keyUsagePattern).ToList();
                var keyUsageByGroup = new Dictionary<int, Dictionary<int, decimal>>();
                
                // Process group spend updates
                foreach (var key in keys)
                {
                    var keyString = key.ToString();
                    var groupId = int.Parse(keyString.Substring(_redisKeyPrefix.Length));
                    
                    // Get and delete atomically
                    var value = await db.StringGetDeleteAsync(key);
                    if (value.HasValue && double.TryParse(value, out var cost))
                    {
                        groupUpdates[groupId] = (decimal)cost;
                    }
                }
                
                // Process key usage data
                foreach (var key in keyUsageKeys)
                {
                    var keyString = key.ToString();
                    var parts = keyString.Split(':');
                    if (parts.Length == 5 && int.TryParse(parts[2], out var groupId) && int.TryParse(parts[4], out var keyId))
                    {
                        var value = await db.StringGetDeleteAsync(key);
                        if (value.HasValue && double.TryParse(value, out var cost))
                        {
                            if (!keyUsageByGroup.ContainsKey(groupId))
                                keyUsageByGroup[groupId] = new Dictionary<int, decimal>();
                            keyUsageByGroup[groupId][keyId] = (decimal)cost;
                        }
                    }
                }
                
                if (groupUpdates.Count() == 0)
                {
                    return 0;
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();
                var groupRepository = scope.ServiceProvider.GetRequiredService<IVirtualKeyGroupRepository>();

                // Process each group
                var updatedKeyHashes = new List<string>();

                foreach (var (groupId, totalCost) in groupUpdates)
                {
                    // Create a description that includes which keys were used
                    var description = "API usage";
                    if (keyUsageByGroup.ContainsKey(groupId))
                    {
                        var keyIds = keyUsageByGroup[groupId].Keys.ToList();
                        if (keyIds.Count() == 1)
                        {
                            description = $"API usage by virtual key #{keyIds[0]}";
                        }
                        else
                        {
                            description = $"API usage by {keyIds.Count()} virtual keys";
                        }
                    }

                    // Update group balance with transaction details
                    // This already creates a transaction record with the correct BalanceAfter
                    var newBalance = await groupRepository.AdjustBalanceAsync(
                        groupId, 
                        -totalCost,
                        description,
                        "System"  // Initiated by system batch process
                    );
                    
                    // Note: We don't need to create additional transaction records here
                    // because AdjustBalanceAsync already creates one with the correct balance.
                    // The individual key usage tracking is already handled in the description.
                    
                    // Get keys in this group for cache invalidation
                    var groupKeys = await context.VirtualKeys
                        .Where(vk => vk.VirtualKeyGroupId == groupId)
                        .Select(vk => new { vk.Id, vk.KeyHash })
                        .ToListAsync();
                    
                    updatedKeyHashes.AddRange(groupKeys.Select(k => k.KeyHash));
                }

                
                _logger.LogInformation("Batch updated spend for {Count} groups", groupUpdates.Count());

                // Raise event for cache invalidation (if any subscribers)
                if (updatedKeyHashes.Count() > 0 && SpendUpdatesCompleted != null)
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

                return groupUpdates.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch spend update");
                throw;
            }
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

            _logger.LogInformation("BatchSpendUpdateService stopping");
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public override void Dispose()
        {
            _flushTimer?.Dispose();
            
            // Try to flush any remaining updates on shutdown
            try
            {
                FlushPendingUpdatesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing pending updates during service disposal");
            }
            
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
                    if (value.HasValue && double.TryParse(value, out var cost))
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