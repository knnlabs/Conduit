using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Virtual Key cache with immediate invalidation for security-critical validation
    /// </summary>
    public class RedisVirtualKeyCache : ConduitLLM.Core.Interfaces.IVirtualKeyCache, IBatchInvalidatable
    {
        private readonly IDatabase _database;
        private readonly ISubscriber _subscriber;
        private readonly ILogger<RedisVirtualKeyCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30); // Fallback expiry
        private const string KeyPrefix = "vkey:";
        private const string InvalidationChannel = "vkey_invalidated";
        private const string BatchInvalidationChannel = "vkey_batch_invalidated";
        
        // Statistics tracking keys
        private const string STATS_HIT_KEY = "conduit:cache:stats:hits";
        private const string STATS_MISS_KEY = "conduit:cache:stats:misses";
        private const string STATS_INVALIDATION_KEY = "conduit:cache:stats:invalidations";
        private const string STATS_RESET_TIME_KEY = "conduit:cache:stats:reset_time";

        public RedisVirtualKeyCache(
            IConnectionMultiplexer redis,
            ILogger<RedisVirtualKeyCache> logger)
        {
            _database = redis.GetDatabase();
            _subscriber = redis.GetSubscriber();
            _logger = logger;

            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(InvalidationChannel), OnKeyInvalidated);
            _subscriber.Subscribe(RedisChannel.Literal(BatchInvalidationChannel), OnBatchInvalidated);
        }

        /// <summary>
        /// Get Virtual Key from cache with immediate fallback to database if not found
        /// </summary>
        /// <param name="keyHash">Hashed key value</param>
        /// <param name="databaseFallback">Function to get from database if cache miss</param>
        /// <returns>Virtual Key if found and valid, null otherwise</returns>
        public async Task<VirtualKey?> GetVirtualKeyAsync(
            string keyHash, 
            Func<string, Task<VirtualKey?>> databaseFallback)
        {
            var cacheKey = KeyPrefix + keyHash;
            
            try
            {
                // Try Redis first - this is ~50x faster than database
                var cachedValue = await _database.StringGetAsync(cacheKey);
                
                if (cachedValue.HasValue)
                {
                    var jsonString = (string?)cachedValue;
                    if (jsonString is not null)
                    {
                        var virtualKey = JsonSerializer.Deserialize<VirtualKey>(jsonString);
                        
                        // Validate key is still enabled and not expired
                        if (virtualKey != null && IsKeyValid(virtualKey))
                        {
                            _logger.LogDebug("Virtual Key cache hit: {KeyHash}", keyHash);
                            // Increment hit counter
                            await _database.StringIncrementAsync(STATS_HIT_KEY);
                            return virtualKey;
                        }
                        else
                        {
                            // Invalid key in cache, remove it
                            await _database.KeyDeleteAsync(cacheKey);
                            _logger.LogDebug("Removed invalid Virtual Key from cache: {KeyHash}", keyHash);
                        }
                    }
                }
                
                // Cache miss or invalid key - fallback to database
                _logger.LogDebug("Virtual Key cache miss, querying database: {KeyHash}", keyHash);
                // Increment miss counter
                await _database.StringIncrementAsync(STATS_MISS_KEY);
                var dbKey = await databaseFallback(keyHash);
                
                if (dbKey != null && IsKeyValid(dbKey))
                {
                    // Cache the valid key
                    await SetVirtualKeyAsync(keyHash, dbKey);
                    return dbKey;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing Virtual Key cache, falling back to database: {KeyHash}", keyHash);
                
                // On any Redis error, fallback to database
                return await databaseFallback(keyHash);
            }
        }

        /// <summary>
        /// Cache a Virtual Key with automatic expiry
        /// </summary>
        /// <param name="keyHash">Hashed key value</param>
        /// <param name="virtualKey">Virtual Key to cache</param>
        public async Task SetVirtualKeyAsync(string keyHash, VirtualKey virtualKey)
        {
            var cacheKey = KeyPrefix + keyHash;
            
            try
            {
                var json = JsonSerializer.Serialize(virtualKey);
                var expiry = CalculateExpiry(virtualKey);
                
                await _database.StringSetAsync(cacheKey, json, expiry);
                
                _logger.LogDebug("Cached Virtual Key: {KeyHash}, expires in {ExpiryMinutes} minutes", 
                    keyHash, expiry.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching Virtual Key: {KeyHash}", keyHash);
                // Don't throw - caching is optimization, not critical path
            }
        }

        /// <summary>
        /// Immediately invalidate a Virtual Key across all instances
        /// CRITICAL: Call this when keys are disabled, deleted, or quota exceeded
        /// </summary>
        /// <param name="keyHash">Hashed key value to invalidate</param>
        public async Task InvalidateVirtualKeyAsync(string keyHash)
        {
            var cacheKey = KeyPrefix + keyHash;
            
            try
            {
                // Remove from local Redis
                await _database.KeyDeleteAsync(cacheKey);
                
                // Notify ALL instances to invalidate their caches
                await _subscriber.PublishAsync(RedisChannel.Literal(InvalidationChannel), keyHash);
                
                // Increment invalidation counter
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY);
                
                _logger.LogInformation("Invalidated Virtual Key across all instances: {KeyHash}", keyHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Virtual Key: {KeyHash}", keyHash);
                throw; // This is critical for security - must not fail silently
            }
        }

        /// <summary>
        /// Bulk invalidate multiple keys (useful for quota updates)
        /// </summary>
        /// <param name="keyHashes">Array of key hashes to invalidate</param>
        public async Task InvalidateVirtualKeysAsync(string[] keyHashes)
        {
            try
            {
                var tasks = new Task[keyHashes.Length];
                for (int i = 0; i < keyHashes.Length; i++)
                {
                    tasks[i] = InvalidateVirtualKeyAsync(keyHashes[i]);
                }
                
                await Task.WhenAll(tasks);
                
                _logger.LogInformation("Bulk invalidated {Count} Virtual Keys", keyHashes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk Virtual Key invalidation");
                throw;
            }
        }

        /// <summary>
        /// Get cache statistics for monitoring
        /// </summary>
        public async Task<ConduitLLM.Core.Interfaces.VirtualKeyCacheStats> GetStatsAsync()
        {
            try
            {
                // Get the actual statistics from Redis
                var hitCountTask = _database.StringGetAsync(STATS_HIT_KEY);
                var missCountTask = _database.StringGetAsync(STATS_MISS_KEY);
                var invalidationCountTask = _database.StringGetAsync(STATS_INVALIDATION_KEY);
                var resetTimeTask = _database.StringGetAsync(STATS_RESET_TIME_KEY);
                
                await Task.WhenAll(hitCountTask, missCountTask, invalidationCountTask, resetTimeTask);
                
                // Parse values with defaults for missing keys
                long hitCount = hitCountTask.Result.HasValue ? (long)hitCountTask.Result : 0;
                long missCount = missCountTask.Result.HasValue ? (long)missCountTask.Result : 0;
                long invalidationCount = invalidationCountTask.Result.HasValue ? (long)invalidationCountTask.Result : 0;
                
                DateTime lastResetTime = DateTime.UtcNow;
                if (resetTimeTask.Result.HasValue)
                {
                    if (long.TryParse(resetTimeTask.Result, out var ticks))
                    {
                        lastResetTime = new DateTime(ticks, DateTimeKind.Utc);
                    }
                }
                else
                {
                    // If no reset time exists, set it now
                    await _database.StringSetAsync(STATS_RESET_TIME_KEY, DateTime.UtcNow.Ticks.ToString());
                }
                
                return new ConduitLLM.Core.Interfaces.VirtualKeyCacheStats
                {
                    HitCount = hitCount,
                    MissCount = missCount,
                    InvalidationCount = invalidationCount,
                    AverageGetTime = TimeSpan.Zero, // Not tracked in this implementation
                    LastResetTime = lastResetTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return new ConduitLLM.Core.Interfaces.VirtualKeyCacheStats();
            }
        }

        /// <summary>
        /// Handle invalidation messages from other instances
        /// </summary>
        private async void OnKeyInvalidated(RedisChannel channel, RedisValue keyHash)
        {
            try
            {
                var cacheKey = KeyPrefix + keyHash;
                await _database.KeyDeleteAsync(cacheKey);
                
                _logger.LogDebug("Invalidated Virtual Key from pub/sub: {KeyHash}", keyHash.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling key invalidation: {KeyHash}", keyHash.ToString());
            }
        }

        /// <summary>
        /// Validate that a Virtual Key is still usable
        /// </summary>
        private static bool IsKeyValid(VirtualKey key)
        {
            // Note: Group balance validation happens at the service layer
            // The cache only validates basic key properties
            return key.IsEnabled && 
                   (key.ExpiresAt == null || key.ExpiresAt > DateTime.UtcNow);
        }

        /// <summary>
        /// Calculate appropriate cache expiry based on key properties
        /// </summary>
        private TimeSpan CalculateExpiry(VirtualKey key)
        {
            // If key expires soon, don't cache for long
            if (key.ExpiresAt.HasValue)
            {
                var timeUntilExpiry = key.ExpiresAt.Value - DateTime.UtcNow;
                if (timeUntilExpiry < _defaultExpiry)
                {
                    return timeUntilExpiry;
                }
            }
            
            // Note: Budget tracking is now at the group level, so we can't check it here
            // The service layer will invalidate keys when group balance is depleted
            
            return _defaultExpiry;
        }

        /// <summary>
        /// Batch invalidate multiple virtual keys for optimal performance
        /// </summary>
        public async Task<BatchInvalidationResult> InvalidateBatchAsync(
            IEnumerable<InvalidationRequest> requests, 
            CancellationToken cancellationToken = default)
        {
            var keyHashes = requests
                .Where(r => r.EntityType == CacheType.VirtualKey.ToString())
                .Select(r => KeyPrefix + r.EntityId)
                .ToArray();
            
            if (keyHashes.Length == 0)
            {
                return new BatchInvalidationResult 
                { 
                    Success = true,
                    ProcessedCount = 0,
                    Duration = TimeSpan.Zero
                };
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Use Redis pipeline for batch delete
                var batch = _database.CreateBatch();
                var deleteTasks = new List<Task<bool>>();
                
                foreach (var key in keyHashes)
                {
                    deleteTasks.Add(batch.KeyDeleteAsync(key));
                }
                
                // Execute batch
                batch.Execute();
                
                // Wait for all deletes to complete
                await Task.WhenAll(deleteTasks);
                
                // Update invalidation statistics
                await _database.StringIncrementAsync(STATS_INVALIDATION_KEY, keyHashes.Length);
                
                // Publish batch invalidation message to other instances
                var batchMessage = new VirtualKeyBatchInvalidation
                {
                    KeyHashes = keyHashes.Select(k => k.Replace(KeyPrefix, "")).ToArray(),
                    Timestamp = DateTime.UtcNow
                };
                
                await _subscriber.PublishAsync(
                    RedisChannel.Literal(BatchInvalidationChannel), 
                    JsonSerializer.Serialize(batchMessage));
                
                stopwatch.Stop();
                
                _logger.LogInformation(
                    "Batch invalidated {Count} virtual keys in {Duration}ms",
                    keyHashes.Length, 
                    stopwatch.ElapsedMilliseconds);
                
                return new BatchInvalidationResult
                {
                    Success = true,
                    ProcessedCount = keyHashes.Length,
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to batch invalidate virtual keys");
                
                return new BatchInvalidationResult
                {
                    Success = false,
                    ProcessedCount = 0,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Handle batch invalidation messages from other instances
        /// </summary>
        private async void OnBatchInvalidated(RedisChannel channel, RedisValue message)
        {
            try
            {
                var batchMessage = JsonSerializer.Deserialize<VirtualKeyBatchInvalidation>(message!);
                if (batchMessage?.KeyHashes != null)
                {
                    var batch = _database.CreateBatch();
                    var deleteTasks = new List<Task<bool>>();
                    
                    foreach (var keyHash in batchMessage.KeyHashes)
                    {
                        var cacheKey = KeyPrefix + keyHash;
                        deleteTasks.Add(batch.KeyDeleteAsync(cacheKey));
                    }
                    
                    batch.Execute();
                    await Task.WhenAll(deleteTasks);
                    
                    _logger.LogDebug(
                        "Batch invalidated {Count} virtual keys from pub/sub",
                        batchMessage.KeyHashes.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling batch key invalidation");
            }
        }

        /// <summary>
        /// Message for batch invalidation pub/sub
        /// </summary>
        private class VirtualKeyBatchInvalidation
        {
            public string[] KeyHashes { get; set; } = Array.Empty<string>();
            public DateTime Timestamp { get; set; }
        }
    }
}