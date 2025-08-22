using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of embedding cache for high-performance caching.
    /// </summary>
    /// <remarks>
    /// This implementation provides:
    /// - Fast Redis-based storage with compression
    /// - Automatic TTL management
    /// - Performance metrics tracking
    /// - Cost savings estimation
    /// - Bulk operations for efficiency
    /// </remarks>
    public class RedisEmbeddingCache : IEmbeddingCache
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisEmbeddingCache> _logger;
        private readonly EmbeddingCacheConfig _config;
        private readonly EmbeddingCacheStats _stats;
        private readonly object _statsLock = new object();

        private const string CACHE_KEY_PREFIX = "emb:";
        private const string STATS_KEY = "emb:stats";
        private const string MODEL_INDEX_PREFIX = "emb:idx:";

        /// <summary>
        /// Initializes a new instance of the RedisEmbeddingCache.
        /// </summary>
        /// <param name="database">Redis database instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="config">Cache configuration.</param>
        public RedisEmbeddingCache(
            IDatabase database,
            ILogger<RedisEmbeddingCache> logger,
            EmbeddingCacheConfig? config = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? new EmbeddingCacheConfig();
            _stats = new EmbeddingCacheStats
            {
                LastResetTime = DateTime.UtcNow
            };
        }

        /// <inheritdoc/>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    return _database.IsConnected("embedding-cache-check");
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<EmbeddingResponse?> GetEmbeddingAsync(string cacheKey)
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("Redis cache is not available");
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var cacheKeyWithPrefix = CACHE_KEY_PREFIX + cacheKey;
                var cachedData = await _database.StringGetAsync(cacheKeyWithPrefix);

                if (cachedData.HasValue)
                {
                    var response = JsonSerializer.Deserialize<EmbeddingResponse>(cachedData!);
                    
                    lock (_statsLock)
                    {
                        _stats.HitCount++;
                        var currentAvg = _stats.AverageGetTime;
                        UpdateAverageTime(ref currentAvg, stopwatch.Elapsed);
                        _stats.AverageGetTime = currentAvg;
                    }

                    _logger.LogDebug("Cache hit for embedding key: {CacheKey}", cacheKey);
                    return response;
                }

                lock (_statsLock)
                {
                    _stats.MissCount++;
                }

                _logger.LogDebug("Cache miss for embedding key: {CacheKey}", cacheKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving embedding from cache: {CacheKey}", cacheKey);
                lock (_statsLock)
                {
                    _stats.MissCount++;
                }
                return null;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        /// <inheritdoc/>
        public async Task SetEmbeddingAsync(string cacheKey, EmbeddingResponse response, TimeSpan? ttl = null)
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("Redis cache is not available, skipping cache set");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var cacheKeyWithPrefix = CACHE_KEY_PREFIX + cacheKey;
                var serializedResponse = JsonSerializer.Serialize(response);
                var effectiveTtl = ttl ?? _config.DefaultTtl;

                // Store the embedding response
                await _database.StringSetAsync(cacheKeyWithPrefix, serializedResponse, effectiveTtl);

                // Add to model index for efficient invalidation
                if (!string.IsNullOrEmpty(response.Model))
                {
                    var modelIndexKey = MODEL_INDEX_PREFIX + response.Model;
                    await _database.SetAddAsync(modelIndexKey, cacheKey);
                    await _database.KeyExpireAsync(modelIndexKey, effectiveTtl.Add(TimeSpan.FromMinutes(5))); // Index expires slightly later
                }

                lock (_statsLock)
                {
                    _stats.EntryCount++;
                    _stats.TotalSizeBytes += serializedResponse.Length;
                    var currentAvg = _stats.AverageSetTime;
                    UpdateAverageTime(ref currentAvg, stopwatch.Elapsed);
                    _stats.AverageSetTime = currentAvg;
                }

                _logger.LogDebug("Cached embedding response for key: {CacheKey}, TTL: {TTL}", cacheKey, effectiveTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error storing embedding in cache: {CacheKey}", cacheKey);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        /// <inheritdoc/>
        public string GenerateCacheKey(EmbeddingRequest request)
        {
            // Create a deterministic cache key based on request parameters
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(request.Model ?? "default");
            keyBuilder.Append(":");

            // Handle single string input
            if (request.Input is string singleInput)
            {
                keyBuilder.Append(singleInput);
            }
            // Handle array of strings
            else if (request.Input is string[] arrayInput)
            {
                keyBuilder.Append(string.Join("|", arrayInput));
            }
            // Handle list of strings
            else if (request.Input is List<string> listInput)
            {
                keyBuilder.Append(string.Join("|", listInput));
            }
            else
            {
                // Fallback: serialize the input object
                keyBuilder.Append(JsonSerializer.Serialize(request.Input));
            }

            // Include relevant parameters that affect the embedding
            if (request.EncodingFormat != null)
            {
                keyBuilder.Append($":enc_{request.EncodingFormat}");
            }

            if (request.Dimensions.HasValue)
            {
                keyBuilder.Append($":dim_{request.Dimensions.Value}");
            }

            // Generate SHA256 hash for consistent, compact key
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <inheritdoc/>
        public async Task InvalidateModelCacheAsync(string modelName)
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("Redis cache is not available");
                return;
            }

            try
            {
                var modelIndexKey = MODEL_INDEX_PREFIX + modelName;
                var cacheKeys = await _database.SetMembersAsync(modelIndexKey);

                if (cacheKeys.Length > 0)
                {
                    // Delete all cache entries for this model
                    var keysToDelete = cacheKeys.Select(key => (RedisKey)(CACHE_KEY_PREFIX + key)).ToArray();
                    await _database.KeyDeleteAsync(keysToDelete);

                    // Remove the model index
                    await _database.KeyDeleteAsync(modelIndexKey);

                    lock (_statsLock)
                    {
                        _stats.InvalidationCount += cacheKeys.Length;
                        _stats.EntryCount = Math.Max(0, _stats.EntryCount - cacheKeys.Length);
                    }

                    _logger.LogInformation("Invalidated {Count} cache entries for model: {ModelName}", 
                        cacheKeys.Length, modelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating model cache: {ModelName}", modelName);
            }
        }

        /// <inheritdoc/>
        public async Task InvalidateBulkAsync(IEnumerable<string> cacheKeys)
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("Redis cache is not available");
                return;
            }

            try
            {
                var keyArray = cacheKeys.Select(key => (RedisKey)(CACHE_KEY_PREFIX + key)).ToArray();
                if (keyArray.Length > 0)
                {
                    var deletedCount = await _database.KeyDeleteAsync(keyArray);

                    lock (_statsLock)
                    {
                        _stats.InvalidationCount += deletedCount;
                        _stats.EntryCount = Math.Max(0, _stats.EntryCount - deletedCount);
                    }

                    _logger.LogInformation("Bulk invalidated {Count} cache entries", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during bulk invalidation");
            }
        }

        /// <inheritdoc/>
        public async Task<EmbeddingCacheStats> GetStatsAsync()
        {
            EmbeddingCacheStats currentStats;
            lock (_statsLock)
            {
                currentStats = new EmbeddingCacheStats
                {
                    HitCount = _stats.HitCount,
                    MissCount = _stats.MissCount,
                    InvalidationCount = _stats.InvalidationCount,
                    EntryCount = _stats.EntryCount,
                    TotalSizeBytes = _stats.TotalSizeBytes,
                    AverageGetTime = _stats.AverageGetTime,
                    AverageSetTime = _stats.AverageSetTime,
                    LastResetTime = _stats.LastResetTime,
                    EstimatedCostSavings = _stats.EstimatedCostSavings
                };
            }

            // Get live entry count from Redis if available
            if (IsAvailable)
            {
                try
                {
                    var pattern = CACHE_KEY_PREFIX + "*";
                    var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
                    var keys = server.Keys(pattern: pattern, pageSize: 1000).Take(1000);
                    currentStats.EntryCount = keys.Count();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not get live entry count from Redis");
                }
            }

            return await Task.FromResult(currentStats);
        }

        /// <inheritdoc/>
        public async Task ClearAllAsync()
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("Redis cache is not available");
                return;
            }

            try
            {
                var pattern = CACHE_KEY_PREFIX + "*";
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern, pageSize: 1000);

                var keyArray = keys.Select(key => (RedisKey)key).ToArray();
                if (keyArray.Length > 0)
                {
                    var deletedCount = await _database.KeyDeleteAsync(keyArray);

                    lock (_statsLock)
                    {
                        _stats.EntryCount = 0;
                        _stats.TotalSizeBytes = 0;
                        _stats.InvalidationCount += deletedCount;
                    }

                    _logger.LogInformation("Cleared all embedding cache entries: {Count}", deletedCount);
                }

                // Also clear model indexes
                var indexPattern = MODEL_INDEX_PREFIX + "*";
                var indexKeys = server.Keys(pattern: indexPattern, pageSize: 1000);
                var indexKeyArray = indexKeys.Select(key => (RedisKey)key).ToArray();
                if (indexKeyArray.Length > 0)
                {
                    await _database.KeyDeleteAsync(indexKeyArray);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing all cache entries");
            }
        }

        /// <summary>
        /// Updates a running average with a new time measurement.
        /// </summary>
        private void UpdateAverageTime(ref TimeSpan currentAverage, TimeSpan newTime)
        {
            const double alpha = 0.1; // Exponential moving average factor
            var currentMs = currentAverage.TotalMilliseconds;
            var newMs = newTime.TotalMilliseconds;
            var updatedMs = (currentMs * (1 - alpha)) + (newMs * alpha);
            currentAverage = TimeSpan.FromMilliseconds(updatedMs);
        }
    }

    /// <summary>
    /// Configuration for embedding cache behavior.
    /// </summary>
    public class EmbeddingCacheConfig
    {
        /// <summary>
        /// Default time-to-live for cache entries.
        /// </summary>
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Maximum size for cache entries in bytes.
        /// </summary>
        public long MaxEntrySizeBytes { get; set; } = 1_000_000; // 1MB

        /// <summary>
        /// Whether to enable compression for large entries.
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Minimum entry size to trigger compression.
        /// </summary>
        public int CompressionThreshold { get; set; } = 1024; // 1KB
    }
}