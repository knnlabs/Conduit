using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Caching;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service that provides status information about the LLM response cache
    /// </summary>
    public class CacheStatusService : ICacheStatusService, IDisposable
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<CacheStatusService> _logger;
        private readonly ICacheMetricsService _metricsService;
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly Configuration.Services.ICacheService _cacheService;
        
        private const string CACHE_CONFIG_KEY = "CacheConfig";
        private Timer? _statisticsTimer;
        private CacheConfig? _lastLoadedConfig;
        private readonly SemaphoreSlim _configLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Creates a new instance of the CacheStatusService
        /// </summary>
        public CacheStatusService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            Configuration.Services.ICacheService cacheService,
            ICacheMetricsService metricsService,
            IOptions<CacheOptions> cacheOptions,
            ILogger<CacheStatusService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize cache settings asynchronously - with error handling
            _ = Task.Run(async () => 
            {
                try 
                {
                    await InitializeCacheAsync();
                    
                    // Start a timer to periodically update cache statistics
                    _statisticsTimer = new Timer(async _ => 
                    {
                        try 
                        {
                            await SaveStatisticsToConfigAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in statistics timer callback");
                        }
                    }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing cache settings");
                }
            });
        }
        
        /// <inheritdoc/>
        public Task<CacheStatus> GetCacheStatusAsync()
        {
            try
            {
                var options = _cacheOptions.Value;
                
                // Get current metrics
                var totalRequests = _metricsService.GetTotalRequests();
                var hitRate = _metricsService.GetHitRate();
                var avgResponseTime = _metricsService.GetAverageRetrievalTimeMs();
                
                // If we don't have any metrics yet but have persisted statistics, use those
                if (totalRequests == 0 && _lastLoadedConfig != null)
                {
                    _logger.LogDebug("Using persisted cache statistics from database");
                    
                    return Task.FromResult(new CacheStatus
                    {
                        IsEnabled = options.IsEnabled,
                        CacheType = options.CacheType,
                        TotalItems = _lastLoadedConfig.TotalItems,
                        HitRate = _lastLoadedConfig.HitRate,
                        MemoryUsageBytes = _lastLoadedConfig.MemoryUsageBytes,
                        AvgResponseTime = _lastLoadedConfig.AvgResponseTimeMs
                    });
                }
                
                // Use current metrics
                return Task.FromResult(new CacheStatus
                {
                    IsEnabled = options.IsEnabled,
                    CacheType = options.CacheType,
                    TotalItems = (int)totalRequests,
                    HitRate = hitRate,
                    MemoryUsageBytes = EstimateMemoryUsage(options),
                    AvgResponseTime = avgResponseTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache status");
                return Task.FromResult(new CacheStatus
                {
                    IsEnabled = false,
                    CacheType = "Unknown",
                    TotalItems = 0,
                    HitRate = 0,
                    MemoryUsageBytes = 0,
                    AvgResponseTime = 0
                });
            }
        }
        
        /// <inheritdoc/>
        public async Task SetCacheEnabledAsync(bool enabled)
        {
            try
            {
                // Update options and save to database
                var options = _cacheOptions.Value;
                options.IsEnabled = enabled;
                
                await SaveCacheConfigAsync();
                
                _logger.LogInformation("Cache {Status}", enabled ? "enabled" : "disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache enabled state to {Enabled}", enabled);
            }
        }
        
        /// <inheritdoc/>
        public async Task ClearCacheAsync()
        {
            try
            {
                // Clear the entire LLM response cache
                _cacheService.RemoveByPrefix("llm:");
                
                // Reset the metrics
                _metricsService.Reset();
                
                // Save updated (empty) statistics
                await SaveStatisticsToConfigAsync();
                
                _logger.LogInformation("Cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }
        
        /// <summary>
        /// Initializes the cache from configuration
        /// </summary>
        private async Task InitializeCacheAsync()
        {
            try
            {
                await _configLock.WaitAsync();
                
                try
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    
                    // Get cache configuration from settings
                    var cacheSetting = await dbContext.GlobalSettings
                        .FirstOrDefaultAsync(s => s.Key == CACHE_CONFIG_KEY);
                    
                    if (cacheSetting != null)
                    {
                        try
                        {
                            // Parse cache settings
                            var config = JsonSerializer.Deserialize<CacheConfig>(cacheSetting.Value);
                            
                            if (config != null)
                            {
                                _lastLoadedConfig = config;
                                
                                // Update the options with stored values
                                var options = _cacheOptions.Value;
                                options.IsEnabled = config.IsEnabled;
                                options.CacheType = config.CacheType;
                                options.DefaultExpirationMinutes = config.DefaultExpirationMinutes;
                                options.MaxCacheItems = config.MaxCacheItems;
                                options.RedisConnectionString = config.RedisConnectionString;
                                options.RedisInstanceName = config.RedisInstanceName;
                                options.IncludeModelInKey = config.IncludeModelInKey;
                                options.IncludeProviderInKey = config.IncludeProviderInKey;
                                options.IncludeApiKeyInKey = config.IncludeApiKeyInKey;
                                options.IncludeTemperatureInKey = config.IncludeTemperatureInKey;
                                options.IncludeMaxTokensInKey = config.IncludeMaxTokensInKey;
                                options.IncludeTopPInKey = config.IncludeTopPInKey;
                                options.HashAlgorithm = config.HashAlgorithm;
                                
                                // Set model rules if available
                                if (config.ModelRules != null && config.ModelRules.Count > 0)
                                {
                                    options.ModelSpecificRules ??= new List<Configuration.Options.ModelCacheRule>();
                                    options.ModelSpecificRules.Clear();
                                    
                                    foreach (var rule in config.ModelRules)
                                    {
                                        options.ModelSpecificRules.Add(new Configuration.Options.ModelCacheRule
                                        {
                                            ModelNamePattern = rule.ModelNamePattern,
                                            CacheBehavior = (Configuration.Options.CacheBehavior)rule.CacheBehavior,
                                            ExpirationMinutes = rule.ExpirationMinutes
                                        });
                                    }
                                }
                                
                                // Initialize metrics service with persisted values if it doesn't have data yet
                                if (_metricsService.GetTotalRequests() == 0 && config.TotalRequests > 0)
                                {
                                    _logger.LogInformation("Initializing cache metrics from persisted data. Total requests: {TotalRequests}, Hit rate: {HitRate}%", 
                                        config.TotalRequests, config.HitRate * 100);
                                    
                                    // Convert model-specific stats to dictionary for import
                                    Dictionary<string, ModelCacheMetrics>? modelMetrics = null;
                                    
                                    if (config.ModelStats != null && config.ModelStats.Count > 0)
                                    {
                                        modelMetrics = new Dictionary<string, ModelCacheMetrics>();
                                        
                                        foreach (var modelStat in config.ModelStats)
                                        {
                                            modelMetrics[modelStat.ModelName] = new ModelCacheMetrics
                                            {
                                                Hits = modelStat.Hits,
                                                Misses = modelStat.Misses,
                                                TotalRetrievalTimeMs = modelStat.TotalRetrievalTimeMs
                                            };
                                        }
                                        
                                        _logger.LogInformation("Importing statistics for {Count} models", modelMetrics.Count);
                                    }
                                    
                                    // Update the metrics service with persisted values including model-specific metrics
                                    _metricsService.ImportStats(
                                        config.TotalHits,
                                        config.TotalMisses,
                                        config.AvgResponseTimeMs,
                                        modelMetrics);
                                }
                                
                                _logger.LogInformation("Cache initialized from database settings");
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Error deserializing cache configuration, using defaults");
                        }
                    }
                    else
                    {
                        // Create default cache settings
                        await SaveCacheConfigAsync();
                        _logger.LogInformation("Default cache configuration created");
                    }
                }
                finally
                {
                    _configLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing cache");
                if (_configLock.CurrentCount == 0)
                {
                    _configLock.Release();
                }
            }
        }
        
        /// <summary>
        /// Saves the cache configuration to the database
        /// </summary>
        private async Task SaveCacheConfigAsync()
        {
            try
            {
                await _configLock.WaitAsync();
                
                try
                {
                    // Get the latest metrics before saving
                    await SaveStatisticsToConfigAsync(includeConfig: true);
                }
                finally
                {
                    _configLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cache configuration");
                if (_configLock.CurrentCount == 0)
                {
                    _configLock.Release();
                }
            }
        }
        
        /// <summary>
        /// Saves the current cache statistics to the configuration
        /// </summary>
        private async Task SaveStatisticsToConfigAsync(bool includeConfig = false)
        {
            try
            {
                await _configLock.WaitAsync();
                
                try
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    
                    // Get existing config or create new one
                    var cacheSetting = await dbContext.GlobalSettings
                        .FirstOrDefaultAsync(s => s.Key == CACHE_CONFIG_KEY);
                    
                    CacheConfig config;
                    if (cacheSetting != null)
                    {
                        // Use existing config
                        config = JsonSerializer.Deserialize<CacheConfig>(cacheSetting.Value) ?? new CacheConfig();
                    }
                    else
                    {
                        // Create new default config
                        config = new CacheConfig();
                        cacheSetting = new GlobalSetting
                        {
                            Key = CACHE_CONFIG_KEY,
                            Value = "{}"
                        };
                        dbContext.GlobalSettings.Add(cacheSetting);
                    }
                    
                    var options = _cacheOptions.Value;
                    
                    // Only update configuration properties if requested
                    if (includeConfig)
                    {
                        config.IsEnabled = options.IsEnabled;
                        config.CacheType = options.CacheType;
                        config.DefaultExpirationMinutes = options.DefaultExpirationMinutes;
                        config.MaxCacheItems = options.MaxCacheItems;
                        config.RedisConnectionString = options.RedisConnectionString;
                        config.RedisInstanceName = options.RedisInstanceName;
                        config.IncludeModelInKey = options.IncludeModelInKey;
                        config.IncludeProviderInKey = options.IncludeProviderInKey;
                        config.IncludeApiKeyInKey = options.IncludeApiKeyInKey;
                        config.IncludeTemperatureInKey = options.IncludeTemperatureInKey;
                        config.IncludeMaxTokensInKey = options.IncludeMaxTokensInKey;
                        config.IncludeTopPInKey = options.IncludeTopPInKey;
                        config.HashAlgorithm = options.HashAlgorithm;
                        
                        // Convert model rules
                        config.ModelRules.Clear();
                        if (options.ModelSpecificRules != null && options.ModelSpecificRules.Count > 0)
                        {
                            foreach (var rule in options.ModelSpecificRules)
                            {
                                config.ModelRules.Add(new ModelRuleConfig
                                {
                                    ModelNamePattern = rule.ModelNamePattern,
                                    CacheBehavior = (int)rule.CacheBehavior,
                                    ExpirationMinutes = rule.ExpirationMinutes
                                });
                            }
                        }
                    }
                    
                    // Always update statistics
                    config.TotalItems = (int)_metricsService.GetTotalRequests();
                    config.HitRate = _metricsService.GetHitRate();
                    config.MemoryUsageBytes = EstimateMemoryUsage(options);
                    config.AvgResponseTimeMs = _metricsService.GetAverageRetrievalTimeMs();
                    config.TotalHits = _metricsService.GetTotalHits();
                    config.TotalMisses = _metricsService.GetTotalMisses();
                    config.TotalRequests = _metricsService.GetTotalRequests();
                    config.LastUpdated = DateTime.UtcNow;
                    
                    // Update model-specific statistics
                    config.ModelStats.Clear();
                    
                    var modelMetrics = _metricsService.GetModelMetrics();
                    var trackedModels = _metricsService.GetTrackedModels();
                    
                    foreach (var modelName in trackedModels)
                    {
                        var metrics = _metricsService.GetMetricsForModel(modelName);
                        if (metrics != null)
                        {
                            config.ModelStats.Add(new ModelStatsConfig
                            {
                                ModelName = modelName,
                                Hits = metrics.Hits,
                                Misses = metrics.Misses,
                                TotalRetrievalTimeMs = metrics.TotalRetrievalTimeMs
                            });
                        }
                    }
                    
                    // Save the updated config
                    cacheSetting.Value = JsonSerializer.Serialize(config);
                    await dbContext.SaveChangesAsync();
                    
                    // Update the cached config
                    _lastLoadedConfig = config;
                    
                    _logger.LogDebug("Cache statistics saved to database. Total requests: {TotalRequests}, Hit rate: {HitRate}%", 
                        config.TotalRequests, config.HitRate * 100);
                }
                finally
                {
                    _configLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cache statistics");
                if (_configLock.CurrentCount == 0)
                {
                    _configLock.Release();
                }
            }
        }
        
        /// <summary>
        /// Estimates the memory usage of the cache based on configuration
        /// </summary>
        private long EstimateMemoryUsage(CacheOptions options)
        {
            if (!options.IsEnabled)
                return 0;
                
            if (options.CacheType == "Redis")
            {
                // For Redis, we don't have direct memory usage info
                // so we make a rough estimate based on item count and average size
                const int estimatedBytesPerCachedResponse = 2048; // Rough estimate of average response size
                return _metricsService.GetTotalRequests() * estimatedBytesPerCachedResponse;
            }
            else // Memory cache
            {
                // Estimate based on item count and a rough per-item size
                const int estimatedBytesPerCachedResponse = 4096; // In-memory cache has more overhead
                const long baselineUsage = 2 * 1024 * 1024; // 2MB baseline for the cache infrastructure
                
                return baselineUsage + (_metricsService.GetTotalRequests() * estimatedBytesPerCachedResponse);
            }
        }
        
        /// <summary>
        /// Disposes of resources used by the service
        /// </summary>
        public void Dispose()
        {
            // Stop and dispose of the timer
            _statisticsTimer?.Dispose();
            _configLock.Dispose();
        }
        
        /// <summary>
        /// Simple configuration class for cache settings stored in the database
        /// </summary>
        private class CacheConfig
        {
            // Configuration Properties
            public bool IsEnabled { get; set; } = false;
            public string CacheType { get; set; } = "Memory";
            public int DefaultExpirationMinutes { get; set; } = 60;
            public int MaxCacheItems { get; set; } = 10000;
            public string RedisConnectionString { get; set; } = string.Empty;
            public string RedisInstanceName { get; set; } = "conduitllm-cache";
            public bool IncludeModelInKey { get; set; } = true;
            public bool IncludeProviderInKey { get; set; } = true;
            public bool IncludeApiKeyInKey { get; set; } = false;
            public bool IncludeTemperatureInKey { get; set; } = true;
            public bool IncludeMaxTokensInKey { get; set; } = false;
            public bool IncludeTopPInKey { get; set; } = false;
            public string HashAlgorithm { get; set; } = "MD5";
            public System.Collections.Generic.List<ModelRuleConfig> ModelRules { get; set; } = new System.Collections.Generic.List<ModelRuleConfig>();
            
            // Statistics Properties
            public int TotalItems { get; set; } = 0;
            public double HitRate { get; set; } = 0.0;
            public long MemoryUsageBytes { get; set; } = 0;
            public double AvgResponseTimeMs { get; set; } = 0.0;
            public long TotalHits { get; set; } = 0;
            public long TotalMisses { get; set; } = 0;
            public long TotalRequests { get; set; } = 0;
            public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
            
            // Model-specific Statistics
            public System.Collections.Generic.List<ModelStatsConfig> ModelStats { get; set; } = new System.Collections.Generic.List<ModelStatsConfig>();
        }
        
        /// <summary>
        /// Model cache rule configuration for database storage
        /// </summary>
        private class ModelRuleConfig
        {
            public string ModelNamePattern { get; set; } = string.Empty;
            public int CacheBehavior { get; set; } = 0;
            public int? ExpirationMinutes { get; set; }
        }
        
        /// <summary>
        /// Model-specific statistics for database storage
        /// </summary>
        private class ModelStatsConfig
        {
            public string ModelName { get; set; } = string.Empty;
            public long Hits { get; set; } = 0;
            public long Misses { get; set; } = 0;
            public long TotalRetrievalTimeMs { get; set; } = 0;
            public double HitRate => (Hits + Misses) > 0 ? (double)Hits / (Hits + Misses) : 0;
            public double AvgRetrievalTimeMs => Hits > 0 ? (double)TotalRetrievalTimeMs / Hits : 0;
        }
    }
}
