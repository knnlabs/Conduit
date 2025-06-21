using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Redis-based Virtual Key cache with immediate invalidation for security-critical validation
    /// </summary>
    public class RedisVirtualKeyCache : ConduitLLM.Core.Interfaces.IVirtualKeyCache
    {
        private readonly IDatabase _database;
        private readonly ISubscriber _subscriber;
        private readonly ILogger<RedisVirtualKeyCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30); // Fallback expiry
        private const string KeyPrefix = "vkey:";
        private const string InvalidationChannel = "vkey_invalidated";

        public RedisVirtualKeyCache(
            IConnectionMultiplexer redis,
            ILogger<RedisVirtualKeyCache> logger)
        {
            _database = redis.GetDatabase();
            _subscriber = redis.GetSubscriber();
            _logger = logger;

            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(InvalidationChannel), OnKeyInvalidated);
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
                var info = await _database.ExecuteAsync("INFO", "memory");
                var keyCount = await _database.ExecuteAsync("EVAL", 
                    "return #redis.call('keys', ARGV[1])", 0, KeyPrefix + "*");
                
                return new ConduitLLM.Core.Interfaces.VirtualKeyCacheStats
                {
                    HitCount = 0, // TODO: Implement proper statistics tracking
                    MissCount = 0, // TODO: Implement proper statistics tracking
                    InvalidationCount = 0, // TODO: Implement proper statistics tracking
                    AverageGetTime = TimeSpan.Zero,
                    LastResetTime = DateTime.UtcNow
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
            return key.IsEnabled && 
                   (key.ExpiresAt == null || key.ExpiresAt > DateTime.UtcNow) &&
                   (key.MaxBudget == null || key.CurrentSpend < key.MaxBudget);
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
            
            // If key is close to budget limit, shorter cache time
            if (key.MaxBudget.HasValue && key.CurrentSpend > (key.MaxBudget * 0.9m))
            {
                return TimeSpan.FromMinutes(5); // Short cache for near-limit keys
            }
            
            return _defaultExpiry;
        }
    }
}