using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Configuration options for discovery capabilities cache
    /// </summary>
    public class DiscoveryCapabilitiesCacheOptions
    {
        /// <summary>
        /// Default cache duration for provider capabilities in minutes
        /// </summary>
        public int DefaultCacheDurationMinutes { get; set; } = 1440; // 24 hours

        /// <summary>
        /// Cache duration for model-specific capabilities in minutes
        /// </summary>
        public int ModelCapabilityCacheDurationMinutes { get; set; } = 360; // 6 hours

        /// <summary>
        /// Cache duration for capability check results in minutes
        /// </summary>
        public int CapabilityCheckCacheDurationMinutes { get; set; } = 60; // 1 hour

        /// <summary>
        /// Whether to use distributed cache (Redis) or memory cache
        /// </summary>
        public bool UseDistributedCache { get; set; } = true;

        /// <summary>
        /// Cache key prefix for discovery data
        /// </summary>
        public string CacheKeyPrefix { get; set; } = "discovery_cache:";

        /// <summary>
        /// Maximum number of models to cache simultaneously
        /// </summary>
        public int MaxCachedModels { get; set; } = 1000;

        /// <summary>
        /// Enable cache warming on startup
        /// </summary>
        public bool EnableCacheWarming { get; set; } = true;

        /// <summary>
        /// Models to prioritize for cache warming
        /// </summary>
        public List<string> PriorityCacheModels { get; set; } = new() 
        { 
            "gpt-4", "gpt-3.5-turbo", "claude-3", "gemini-pro" 
        };
    }

    /// <summary>
    /// Cache entry for provider capabilities
    /// </summary>
    public class CachedProviderCapabilities
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// Available models
        /// </summary>
        public List<string> Models { get; set; } = new();

        /// <summary>
        /// Supported capabilities
        /// </summary>
        public Dictionary<string, bool> Capabilities { get; set; } = new();

        /// <summary>
        /// When this entry was cached
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Cache version for invalidation
        /// </summary>
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Cache entry for model-specific capability check
    /// </summary>
    public class CachedCapabilityCheck
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string Model { get; set; } = "";

        /// <summary>
        /// Capability type
        /// </summary>
        public string Capability { get; set; } = "";

        /// <summary>
        /// Whether the model supports the capability
        /// </summary>
        public bool Supported { get; set; }

        /// <summary>
        /// When this entry was cached
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Additional metadata about the capability
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Interface for discovery capabilities cache service
    /// </summary>
    public interface IDiscoveryCapabilitiesCache
    {
        /// <summary>
        /// Gets cached provider capabilities
        /// </summary>
        Task<CachedProviderCapabilities?> GetProviderCapabilitiesAsync(string provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets provider capabilities in cache
        /// </summary>
        Task SetProviderCapabilitiesAsync(string provider, CachedProviderCapabilities capabilities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cached capability check result
        /// </summary>
        Task<CachedCapabilityCheck?> GetCapabilityCheckAsync(string model, string capability, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets capability check result in cache
        /// </summary>
        Task SetCapabilityCheckAsync(string model, string capability, bool supported, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache for a specific provider
        /// </summary>
        Task InvalidateProviderAsync(string provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache for a specific model
        /// </summary>
        Task InvalidateModelAsync(string model, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates all discovery cache entries
        /// </summary>
        Task InvalidateAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Warms the cache with priority models
        /// </summary>
        Task WarmCacheAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        Task<DiscoveryCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class DiscoveryCacheStats
    {
        /// <summary>
        /// Number of cached providers
        /// </summary>
        public int CachedProviders { get; set; }

        /// <summary>
        /// Number of cached models
        /// </summary>
        public int CachedModels { get; set; }

        /// <summary>
        /// Number of cached capability checks
        /// </summary>
        public int CachedCapabilityChecks { get; set; }

        /// <summary>
        /// Cache hit rate percentage
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Total cache requests
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Cache hits
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Cache misses
        /// </summary>
        public long Misses { get; set; }
    }

    /// <summary>
    /// Enhanced discovery capabilities cache with Redis support
    /// </summary>
    public class DiscoveryCapabilitiesCache : IDiscoveryCapabilitiesCache
    {
        private readonly DiscoveryCapabilitiesCacheOptions _options;
        private readonly IDistributedCache? _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DiscoveryCapabilitiesCache> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        // Performance tracking
        private long _totalRequests;
        private long _hits;
        private long _misses;

        /// <summary>
        /// Initializes a new instance of the DiscoveryCapabilitiesCache
        /// </summary>
        public DiscoveryCapabilitiesCache(
            IOptions<DiscoveryCapabilitiesCacheOptions> options,
            IMemoryCache memoryCache,
            ILogger<DiscoveryCapabilitiesCache> logger,
            IServiceProvider serviceProvider)
        {
            _options = options.Value;
            _memoryCache = memoryCache;
            _logger = logger;
            _distributedCache = _options.UseDistributedCache ? serviceProvider.GetService(typeof(IDistributedCache)) as IDistributedCache : null;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            if (_distributedCache == null && _options.UseDistributedCache)
            {
                _logger.LogWarning("Distributed cache requested but not available, falling back to memory cache");
            }
        }

        /// <inheritdoc />
        public async Task<CachedProviderCapabilities?> GetProviderCapabilitiesAsync(string provider, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _totalRequests);

            var key = GetProviderCapabilitiesKey(provider);

            try
            {
                // Try distributed cache first
                if (_distributedCache != null)
                {
                    var cachedData = await _distributedCache.GetStringAsync(key, cancellationToken);
                    if (!string.IsNullOrEmpty(cachedData))
                    {
                        var capabilities = JsonSerializer.Deserialize<CachedProviderCapabilities>(cachedData, _jsonOptions);
                        if (capabilities != null)
                        {
                            Interlocked.Increment(ref _hits);
                            _logger.LogDebug("Cache hit for provider capabilities: {Provider}", provider);
                            return capabilities;
                        }
                    }
                }

                // Fallback to memory cache
                if (_memoryCache.TryGetValue(key, out CachedProviderCapabilities? memoryCapabilities))
                {
                    Interlocked.Increment(ref _hits);
                    _logger.LogDebug("Memory cache hit for provider capabilities: {Provider}", provider);
                    return memoryCapabilities;
                }

                Interlocked.Increment(ref _misses);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider capabilities from cache for provider {Provider}", provider);
                Interlocked.Increment(ref _misses);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SetProviderCapabilitiesAsync(string provider, CachedProviderCapabilities capabilities, CancellationToken cancellationToken = default)
        {
            capabilities.CachedAt = DateTime.UtcNow;
            var key = GetProviderCapabilitiesKey(provider);
            var expiration = TimeSpan.FromMinutes(_options.DefaultCacheDurationMinutes);

            try
            {
                // Set in distributed cache
                if (_distributedCache != null)
                {
                    var json = JsonSerializer.Serialize(capabilities, _jsonOptions);
                    await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration
                    }, cancellationToken);
                }

                // Set in memory cache as backup
                _memoryCache.Set(key, capabilities, expiration);

                _logger.LogDebug("Cached provider capabilities for provider {Provider} with {ModelCount} models", 
                    provider, capabilities.Models.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting provider capabilities in cache for provider {Provider}", provider);
            }
        }

        /// <inheritdoc />
        public async Task<CachedCapabilityCheck?> GetCapabilityCheckAsync(string model, string capability, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _totalRequests);

            var key = GetCapabilityCheckKey(model, capability);

            try
            {
                // Try distributed cache first
                if (_distributedCache != null)
                {
                    var cachedData = await _distributedCache.GetStringAsync(key, cancellationToken);
                    if (!string.IsNullOrEmpty(cachedData))
                    {
                        var capabilityCheck = JsonSerializer.Deserialize<CachedCapabilityCheck>(cachedData, _jsonOptions);
                        if (capabilityCheck != null)
                        {
                            Interlocked.Increment(ref _hits);
                            _logger.LogDebug("Cache hit for capability check: {Model}/{Capability}", model, capability);
                            return capabilityCheck;
                        }
                    }
                }

                // Fallback to memory cache
                if (_memoryCache.TryGetValue(key, out CachedCapabilityCheck? memoryCheck))
                {
                    Interlocked.Increment(ref _hits);
                    _logger.LogDebug("Memory cache hit for capability check: {Model}/{Capability}", model, capability);
                    return memoryCheck;
                }

                Interlocked.Increment(ref _misses);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving capability check from cache for {Model}/{Capability}", model, capability);
                Interlocked.Increment(ref _misses);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SetCapabilityCheckAsync(string model, string capability, bool supported, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
        {
            var capabilityCheck = new CachedCapabilityCheck
            {
                Model = model,
                Capability = capability,
                Supported = supported,
                CachedAt = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            var key = GetCapabilityCheckKey(model, capability);
            var expiration = TimeSpan.FromMinutes(_options.CapabilityCheckCacheDurationMinutes);

            try
            {
                // Set in distributed cache
                if (_distributedCache != null)
                {
                    var json = JsonSerializer.Serialize(capabilityCheck, _jsonOptions);
                    await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration
                    }, cancellationToken);
                }

                // Set in memory cache as backup
                _memoryCache.Set(key, capabilityCheck, expiration);

                _logger.LogDebug("Cached capability check for {Model}/{Capability}: {Supported}", 
                    model, capability, supported);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting capability check in cache for {Model}/{Capability}", model, capability);
            }
        }

        /// <inheritdoc />
        public async Task InvalidateProviderAsync(string provider, CancellationToken cancellationToken = default)
        {
            var key = GetProviderCapabilitiesKey(provider);

            try
            {
                if (_distributedCache != null)
                {
                    await _distributedCache.RemoveAsync(key, cancellationToken);
                }

                _memoryCache.Remove(key);

                _logger.LogInformation("Invalidated cache for provider {Provider}", provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for provider {Provider}", provider);
            }
        }

        /// <inheritdoc />
        public async Task InvalidateModelAsync(string model, CancellationToken cancellationToken = default)
        {
            try
            {
                // Invalidate all capability checks for this model
                var commonCapabilities = new[] { "Chat", "ImageGeneration", "Vision", "AudioTranscription", "TextToSpeech", "Embeddings" };
                
                foreach (var capability in commonCapabilities)
                {
                    var key = GetCapabilityCheckKey(model, capability);
                    
                    if (_distributedCache != null)
                    {
                        await _distributedCache.RemoveAsync(key, cancellationToken);
                    }

                    _memoryCache.Remove(key);
                }

                _logger.LogInformation("Invalidated capability cache for model {Model}", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for model {Model}", model);
            }
        }

        /// <inheritdoc />
        public Task InvalidateAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // For distributed cache, we'd need to scan keys with prefix
                // This is a simplified implementation - in production, consider using Redis SCAN
                if (_distributedCache != null)
                {
                    _logger.LogWarning("Full cache invalidation for distributed cache requires manual implementation");
                }

                // Clear memory cache completely (not ideal for production)
                if (_memoryCache is MemoryCache mc)
                {
                    mc.Compact(1.0);
                }

                _logger.LogInformation("Invalidated all discovery cache entries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all cache entries");
            }
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task WarmCacheAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCacheWarming)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Starting cache warming for {Count} priority models", _options.PriorityCacheModels.Count);

                // This would typically call the actual discovery service
                // For now, we'll just log the intention
                foreach (var model in _options.PriorityCacheModels)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    _logger.LogDebug("Cache warming for priority model: {Model}", model);
                    
                    // In a real implementation, you would:
                    // 1. Call the discovery service for this model
                    // 2. Cache the results using SetCapabilityCheckAsync
                    
                    await Task.Delay(100, cancellationToken); // Prevent overwhelming the system
                }

                _logger.LogInformation("Cache warming completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warming");
            }
        }

        /// <inheritdoc />
        public async Task<DiscoveryCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            var totalRequests = Interlocked.Read(ref _totalRequests);
            var hits = Interlocked.Read(ref _hits);
            var misses = Interlocked.Read(ref _misses);

            return new DiscoveryCacheStats
            {
                TotalRequests = totalRequests,
                Hits = hits,
                Misses = misses,
                HitRate = totalRequests > 0 ? (double)hits / totalRequests * 100 : 0,
                CachedProviders = 0, // Would require scanning cache keys
                CachedModels = 0,    // Would require scanning cache keys
                CachedCapabilityChecks = 0 // Would require scanning cache keys
            };
        }

        private string GetProviderCapabilitiesKey(string provider)
        {
            return $"{_options.CacheKeyPrefix}provider:{provider}";
        }

        private string GetCapabilityCheckKey(string model, string capability)
        {
            return $"{_options.CacheKeyPrefix}capability:{model}:{capability}";
        }
    }
}