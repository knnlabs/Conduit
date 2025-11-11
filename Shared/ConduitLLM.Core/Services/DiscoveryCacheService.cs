using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Configuration options for discovery cache service
    /// </summary>
    public class DiscoveryCacheOptions
    {
        /// <summary>
        /// Cache duration in minutes for discovery results
        /// </summary>
        public int CacheDurationMinutes { get; set; } = 360; // 6 hours

        /// <summary>
        /// Whether caching is enabled
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Whether to warm cache on startup
        /// </summary>
        public bool WarmCacheOnStartup { get; set; } = false;

        /// <summary>
        /// Delay in seconds before starting cache warming to allow application to fully start
        /// </summary>
        public int WarmupStartupDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Whether to use distributed lock for cache warming coordination across instances
        /// </summary>
        public bool UseDistributedLockForWarming { get; set; } = true;

        /// <summary>
        /// Timeout in seconds for acquiring distributed lock
        /// </summary>
        public int DistributedLockTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Priority models for cache warming
        /// </summary>
        public List<string> PriorityModels { get; set; } = new() { "gpt-4", "claude-3", "gemini-pro" };

        /// <summary>
        /// Common capability filters to warm
        /// </summary>
        public List<string> WarmupCapabilities { get; set; } = new() { "chat", "vision", "image_generation", "video_generation" };
    }

    /// <summary>
    /// Implementation of discovery cache service
    /// </summary>
    public class DiscoveryCacheService : IDiscoveryCacheService
    {
        private readonly DiscoveryCacheOptions _options;
        private readonly IDistributedCache? _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DiscoveryCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        // Statistics tracking
        private long _totalHits;
        private long _totalMisses;
        private DateTime? _lastInvalidation;
        private DateTime? _lastWarmingTime;

        private const string CACHE_KEY_PREFIX = "discovery:models:";

        public DiscoveryCacheService(
            IOptions<DiscoveryCacheOptions> options,
            IMemoryCache memoryCache,
            ILogger<DiscoveryCacheService> logger,
            IServiceProvider serviceProvider)
        {
            _options = options.Value;
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Try to get distributed cache if available
            _distributedCache = serviceProvider.GetService<IDistributedCache>();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            if (_distributedCache == null)
            {
                _logger.LogInformation("Redis/Distributed cache not available, using memory cache only for discovery results");
            }
        }

        public async Task<DiscoveryModelsResult?> GetDiscoveryResultsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCaching)
            {
                return null;
            }

            try
            {
                // Try distributed cache first (Redis)
                if (_distributedCache != null)
                {
                    var cachedJson = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
                    if (!string.IsNullOrEmpty(cachedJson))
                    {
                        var result = JsonSerializer.Deserialize<DiscoveryModelsResult>(cachedJson, _jsonOptions);
                        if (result != null)
                        {
                            Interlocked.Increment(ref _totalHits);
                            _logger.LogDebug("Discovery cache hit (Redis) for key: {CacheKey}", cacheKey);
                            return result;
                        }
                    }
                }

                // Fallback to memory cache
                if (_memoryCache.TryGetValue<DiscoveryModelsResult>(cacheKey, out var memoryResult))
                {
                    Interlocked.Increment(ref _totalHits);
                    _logger.LogDebug("Discovery cache hit (Memory) for key: {CacheKey}", cacheKey);
                    return memoryResult;
                }

                Interlocked.Increment(ref _totalMisses);
                _logger.LogDebug("Discovery cache miss for key: {CacheKey}", cacheKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving discovery results from cache for key: {CacheKey}", cacheKey);
                Interlocked.Increment(ref _totalMisses);
                return null;
            }
        }

        public async Task SetDiscoveryResultsAsync(string cacheKey, DiscoveryModelsResult results, CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCaching)
            {
                return;
            }

            results.CachedAt = DateTime.UtcNow;
            var expiration = TimeSpan.FromMinutes(_options.CacheDurationMinutes);

            try
            {
                // Set in distributed cache (Redis)
                if (_distributedCache != null)
                {
                    var json = JsonSerializer.Serialize(results, _jsonOptions);
                    await _distributedCache.SetStringAsync(
                        cacheKey,
                        json,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = expiration
                        },
                        cancellationToken);
                    
                    _logger.LogDebug("Cached discovery results in Redis for key: {CacheKey} with {Count} models", cacheKey, results.Count);
                }

                // Also set in memory cache as backup
                _memoryCache.Set(cacheKey, results, expiration);
                
                _logger.LogInformation("Cached discovery results for key: {CacheKey} with {Count} models, expires in {Minutes} minutes",
                    cacheKey, results.Count, _options.CacheDurationMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting discovery results in cache for key: {CacheKey}", cacheKey);
            }
        }

        public async Task InvalidateAllDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _lastInvalidation = DateTime.UtcNow;

                // For Redis, we would need to scan and delete keys with pattern
                // This is a simplified approach - in production, use SCAN with pattern matching
                if (_distributedCache != null)
                {
                    // Invalidate common patterns
                    var commonPatterns = new[]
                    {
                        $"{CACHE_KEY_PREFIX}all",
                        $"{CACHE_KEY_PREFIX}capability:chat",
                        $"{CACHE_KEY_PREFIX}capability:vision",
                        $"{CACHE_KEY_PREFIX}capability:image_generation",
                        $"{CACHE_KEY_PREFIX}capability:video_generation",
                        $"{CACHE_KEY_PREFIX}capability:audio_transcription",
                        $"{CACHE_KEY_PREFIX}capability:text_to_speech",
                        $"{CACHE_KEY_PREFIX}capability:embeddings",
                        $"{CACHE_KEY_PREFIX}capability:function_calling"
                    };

                    foreach (var pattern in commonPatterns)
                    {
                        await _distributedCache.RemoveAsync(pattern, cancellationToken);
                    }
                }

                // Clear memory cache entries with discovery prefix
                // Note: This is a simplified approach - production would use a more sophisticated key tracking
                if (_memoryCache is MemoryCache mc)
                {
                    mc.Compact(0.5); // Compact 50% to remove discovery entries
                }

                _logger.LogInformation("Invalidated all discovery cache entries at {Time}", _lastInvalidation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all discovery cache entries");
            }
        }

        public async Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                _lastInvalidation = DateTime.UtcNow;

                // For production Redis, you would use SCAN command with pattern
                // For now, we'll handle specific patterns
                if (_distributedCache != null && pattern.StartsWith(CACHE_KEY_PREFIX))
                {
                    // Handle wildcard patterns
                    if (pattern.EndsWith("*"))
                    {
                        await InvalidateAllDiscoveryAsync(cancellationToken);
                    }
                    else
                    {
                        await _distributedCache.RemoveAsync(pattern, cancellationToken);
                    }
                }

                _logger.LogInformation("Invalidated discovery cache entries matching pattern: {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating discovery cache with pattern: {Pattern}", pattern);
            }
        }

        public async Task WarmDiscoveryCacheAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.WarmCacheOnStartup || !_options.EnableCaching)
            {
                return;
            }

            try
            {
                _lastWarmingTime = DateTime.UtcNow;
                _logger.LogInformation("Starting discovery cache warming for {CapabilityCount} capabilities", 
                    _options.WarmupCapabilities.Count);

                // Note: Actual warming would require calling the discovery service
                // This is a placeholder for the warming logic
                foreach (var capability in _options.WarmupCapabilities)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    _logger.LogDebug("Would warm cache for capability: {Capability}", capability);
                    // In production: call discovery service and cache results
                    
                    await Task.Delay(100, cancellationToken); // Prevent overwhelming
                }

                _logger.LogInformation("Discovery cache warming completed at {Time}", _lastWarmingTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during discovery cache warming");
            }
        }

        public Task<DiscoveryCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var hits = Interlocked.Read(ref _totalHits);
            var misses = Interlocked.Read(ref _totalMisses);
            var total = hits + misses;

            var stats = new DiscoveryCacheStatistics
            {
                Hits = hits,
                Misses = misses,
                HitRate = total > 0 ? (double)hits / total * 100 : 0,
                CachedEntries = 0, // Would require cache key scanning in production
                LastInvalidation = _lastInvalidation,
                LastWarmingTime = _lastWarmingTime
            };

            return Task.FromResult(stats);
        }

        /// <summary>
        /// Builds cache key for discovery results
        /// </summary>
        public static string BuildCacheKey(string? capability = null, int? virtualKeyId = null)
        {
            if (virtualKeyId.HasValue)
            {
                return capability != null 
                    ? $"{CACHE_KEY_PREFIX}virtualkey:{virtualKeyId}:capability:{capability}"
                    : $"{CACHE_KEY_PREFIX}virtualkey:{virtualKeyId}";
            }

            return capability != null 
                ? $"{CACHE_KEY_PREFIX}capability:{capability}"
                : $"{CACHE_KEY_PREFIX}all";
        }
    }
}