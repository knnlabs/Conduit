using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

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
        /// Whether discovery responses include the operator-configured model pricing.
        /// Virtual keys already observe billed spend in these units, so rates are
        /// derivable either way; operators that resell access at a markup can set
        /// this to false (Discovery:ExposePricing) to keep their cost basis private.
        /// </summary>
        public bool ExposePricing { get; set; } = true;

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
        public List<string> WarmupCapabilities { get; set; } = new()
        {
            "chat",
            "image_input",
            "video_input",
            "audio_input",
            "file_input",
            "image_generation",
            "video_generation"
        };
    }

    /// <summary>
    /// Implementation of discovery cache service using CacheManager for unified cache management
    /// </summary>
    public class DiscoveryCacheService : IDiscoveryCacheService
    {
        private readonly DiscoveryCacheOptions _options;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<DiscoveryCacheService> _logger;

        // Statistics tracking
        private long _totalHits;
        private long _totalMisses;
        private DateTime? _lastInvalidation;
        private DateTime? _lastWarmingTime;

        private const CacheRegion DISCOVERY_REGION = CacheRegion.ModelDiscovery;

        public DiscoveryCacheService(
            IOptions<DiscoveryCacheOptions> options,
            ICacheManager cacheManager,
            ILogger<DiscoveryCacheService> logger)
        {
            _options = options.Value;
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("DiscoveryCacheService initialized using CacheManager with ModelDiscovery region");
        }

        public async Task<DiscoveryModelsResult?> GetDiscoveryResultsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCaching)
            {
                return null;
            }

            try
            {
                var result = await _cacheManager.GetAsync<DiscoveryModelsResult>(cacheKey, DISCOVERY_REGION, cancellationToken);

                if (result != null)
                {
                    Interlocked.Increment(ref _totalHits);
                    _logger.LogDebug("Discovery cache hit for key: {CacheKey}", cacheKey);
                }
                else
                {
                    Interlocked.Increment(ref _totalMisses);
                    _logger.LogDebug("Discovery cache miss for key: {CacheKey}", cacheKey);
                }

                return result;
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
                await _cacheManager.SetAsync(cacheKey, results, DISCOVERY_REGION, expiration, cancellationToken);

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

                // Use CacheManager's ClearRegionAsync for surgical invalidation
                // This uses the tracked keys to remove only discovery entries from both memory and Redis
                await _cacheManager.ClearRegionAsync(DISCOVERY_REGION, cancellationToken);

                _logger.LogInformation("Invalidated all discovery cache entries at {Time} using CacheManager.ClearRegionAsync", _lastInvalidation);
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

                // Use CacheManager's pattern-based invalidation
                if (pattern.EndsWith("*"))
                {
                    // For wildcard patterns, clear the entire region
                    await InvalidateAllDiscoveryAsync(cancellationToken);
                }
                else
                {
                    // For specific keys, use RemoveByPatternAsync
                    await _cacheManager.RemoveByPatternAsync(pattern, DISCOVERY_REGION, cancellationToken);
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
        /// Builds cache key for discovery results.
        /// Note: CacheManager handles region prefixing internally, so we only need the logical key.
        /// </summary>
        public static string BuildCacheKey(string? capability = null, int? virtualKeyId = null, bool includePricing = false)
        {
            // Pricing-bearing entries use a distinct key so cached payloads written while
            // ExposePricing was off (or before pricing existed) are never served as priced.
            var suffix = includePricing ? ":with_pricing" : string.Empty;

            if (virtualKeyId.HasValue)
            {
                return capability != null
                    ? $"virtualkey:{virtualKeyId}:capability:{capability}{suffix}"
                    : $"virtualkey:{virtualKeyId}{suffix}";
            }

            return capability != null
                ? $"capability:{capability}{suffix}"
                : $"all{suffix}";
        }
    }
}
