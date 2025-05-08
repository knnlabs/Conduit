using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Caching;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service that provides status information and management functionality for the LLM response cache
    /// </summary>
    /// <remarks>
    /// The CacheStatusService is responsible for:
    /// - Retrieving current cache metrics and status
    /// - Enabling or disabling the cache
    /// - Clearing the cache
    /// - Persisting cache configuration and statistics to the database
    /// - Periodically updating cache statistics
    /// 
    /// This service uses the repository pattern for data access and integrates with the ICacheMetricsService
    /// for gathering cache performance metrics. It also periodically saves cache statistics to the database
    /// for historical tracking and to persist settings between application restarts.
    /// </remarks>
    public class CacheStatusService : ICacheStatusService, IDisposable
    {
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly ILogger<CacheStatusService> _logger;
        private readonly ICacheMetricsService _metricsService;
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly Configuration.Services.ICacheService _cacheService;
        private readonly IRedisCacheMetricsService? _redisCacheMetrics;
        
        private const string CACHE_CONFIG_KEY = "CacheConfig";
        private Timer? _statisticsTimer;
        private CacheConfig? _lastLoadedConfig;
        private readonly SemaphoreSlim _configLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// Creates a new instance of the CacheStatusService
        /// </summary>
        public CacheStatusService(
            IGlobalSettingRepository globalSettingRepository,
            Configuration.Services.ICacheService cacheService,
            ICacheMetricsService metricsService,
            IOptions<CacheOptions> cacheOptions,
            ILogger<CacheStatusService> logger,
            IRedisCacheMetricsService? redisCacheMetrics = null)
        {
            _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redisCacheMetrics = redisCacheMetrics;
            
            // Initialize cache settings asynchronously - with error handling
            _ = Task.Run(async () => 
            {
                try 
                {
                    // Check for cancellation before starting
                    if (_cts.Token.IsCancellationRequested)
                        return;
                
                    await InitializeCacheAsync(_cts.Token);
                    
                    // Start a timer to periodically update cache statistics
                    _statisticsTimer = new Timer(async _ => 
                    {
                        try 
                        {
                            // Skip if service is disposed or cancellation requested
                            if (_isDisposed || _cts.Token.IsCancellationRequested)
                                return;
                                
                            await SaveStatisticsToConfigAsync(false, _cts.Token);
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
            }, _cts.Token);
        }
        
        /// <inheritdoc/>
        /// <exception cref="Exception">Handles and logs any exceptions that occur during status retrieval</exception>
        public async Task<Models.CacheStatus> GetCacheStatusAsync()
        {
            // Check if service is disposed
            if (_isDisposed)
            {
                return new Models.CacheStatus
                {
                    IsEnabled = false,
                    CacheType = "Unknown",
                    TotalItems = 0,
                    HitRate = 0,
                    MemoryUsageBytes = 0,
                    AvgResponseTime = 0,
                    StatusMessage = "Cache service is disposed"
                };
            }
            
            try
            {
                var options = _cacheOptions.Value;
                var result = new Models.CacheStatus
                {
                    IsEnabled = options.IsEnabled,
                    CacheType = options.CacheType
                };
                
                // Get current metrics
                var totalRequests = _metricsService.GetTotalRequests();
                var hitRate = _metricsService.GetHitRate();
                var avgResponseTime = _metricsService.GetAverageRetrievalTimeMs();
                
                // If we don't have any metrics yet but have persisted statistics, use those
                if (totalRequests == 0 && _lastLoadedConfig != null)
                {
                    _logger.LogDebug("Using persisted cache statistics from database");
                    
                    result.TotalItems = _lastLoadedConfig.TotalItems;
                    result.HitRate = _lastLoadedConfig.HitRate;
                    result.MemoryUsageBytes = _lastLoadedConfig.MemoryUsageBytes;
                    result.AvgResponseTime = _lastLoadedConfig.AvgResponseTimeMs;
                }
                else
                {
                    // Use current metrics
                    result.TotalItems = (int)totalRequests;
                    result.HitRate = hitRate;
                    result.MemoryUsageBytes = EstimateMemoryUsage(options);
                    result.AvgResponseTime = avgResponseTime;
                }
                
                // Check again if service is disposed before potentially lengthy operations
                if (_isDisposed)
                {
                    result.StatusMessage = "Cache service was disposed during status retrieval";
                    return result;
                }
                
                // Add Redis-specific info if using Redis cache
                if (options.CacheType?.ToLowerInvariant() == "redis" && _redisCacheMetrics != null)
                {
                    try
                    {
                        // Check if service is disposed before Redis operations
                        if (_isDisposed)
                        {
                            result.StatusMessage = "Cache service was disposed during Redis status check";
                            return result;
                        }
                        
                        // Get Redis connection status
                        result.IsRedisConnected = await _redisCacheMetrics.IsConnectedAsync();
                        
                        if (result.IsRedisConnected)
                        {
                            // Check if service is disposed before Redis detail operations
                            if (_isDisposed)
                            {
                                result.StatusMessage = "Cache service was disposed during Redis detail retrieval";
                                return result;
                            }
                            
                            // Get Redis client info
                            var clientInfo = await _redisCacheMetrics.GetClientInfoAsync();
                            var memoryStats = await _redisCacheMetrics.GetMemoryStatsAsync();
                            var dbStats = await _redisCacheMetrics.GetDatabaseStatsAsync();
                            var serverInfo = await _redisCacheMetrics.GetServerInfoAsync();
                            
                            // Final check if service is disposed before constructing result
                            if (_isDisposed)
                            {
                                result.StatusMessage = "Cache service was disposed during Redis metrics collection";
                                return result;
                            }
                            
                            // Set Redis connection info
                            result.RedisConnection = new Models.RedisConnectionInfo
                            {
                                ConnectedClients = clientInfo.ConnectedClients,
                                Endpoint = serverInfo.TryGetValue("server:redis_version", out var version) ? 
                                    serverInfo.TryGetValue("server:os", out var os) ? $"{os} (Redis {version})" : $"Redis {version}" : 
                                    "Redis Server",
                                Version = serverInfo.TryGetValue("server:redis_version", out var v) ? v : "Unknown",
                                InstanceName = options.RedisInstanceName
                            };
                            
                            // Set Redis memory info
                            result.RedisMemory = new Models.RedisMemoryInfo
                            {
                                UsedMemory = memoryStats.UsedMemory,
                                PeakMemory = memoryStats.PeakMemory,
                                FragmentationRatio = memoryStats.FragmentationRatio,
                                CachedMemory = memoryStats.CachedMemory
                            };
                            
                            // Set Redis database info
                            result.RedisDatabase = new Models.RedisDatabaseInfo
                            {
                                KeyCount = dbStats.KeyCount,
                                ExpiredKeys = dbStats.ExpiredKeys,
                                EvictedKeys = dbStats.EvictedKeys,
                                Hits = dbStats.Hits,
                                Misses = dbStats.Misses,
                                HitRatePercentage = dbStats.HitRate
                            };
                            
                            // Update memory usage from Redis stats
                            if (memoryStats.UsedMemory > 0)
                            {
                                result.MemoryUsageBytes = memoryStats.UsedMemory;
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        result.IsRedisConnected = false;
                        result.StatusMessage = "Redis services were disposed during metrics collection";
                    }
                    catch (Exception redisEx)
                    {
                        _logger.LogWarning(redisEx, "Error getting Redis metrics");
                        result.IsRedisConnected = false;
                        result.StatusMessage = $"Redis metrics error: {redisEx.Message}";
                    }
                }
                
                return result;
            }
            catch (ObjectDisposedException)
            {
                return new Models.CacheStatus
                {
                    IsEnabled = false,
                    CacheType = "Unknown",
                    TotalItems = 0,
                    HitRate = 0,
                    MemoryUsageBytes = 0,
                    AvgResponseTime = 0,
                    StatusMessage = "Cache service was disposed during status retrieval"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache status. Exception details: {Message}", ex.Message);
                // Log more details for debugging
                _logger.LogDebug("Cache status request details - Options enabled: {IsEnabled}, Cache type: {CacheType}, Metrics service available: {MetricsAvailable}",
                    _cacheOptions.Value?.IsEnabled ?? false,
                    _cacheOptions.Value?.CacheType ?? "Unknown",
                    _metricsService != null);
                    
                return new Models.CacheStatus
                {
                    IsEnabled = false,
                    CacheType = "Unknown",
                    TotalItems = 0,
                    HitRate = 0,
                    MemoryUsageBytes = 0,
                    AvgResponseTime = 0,
                    StatusMessage = $"Error: {ex.Message}"
                };
            }
        }
        
        /// <inheritdoc/>
        /// <exception cref="Exception">Handles and logs any exceptions that occur during operation</exception>
        public async Task SetCacheEnabledAsync(bool enabled)
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
                
            try
            {
                // Update options and save to database
                var options = _cacheOptions.Value;
                options.IsEnabled = enabled;
                
                await SaveCacheConfigAsync(_cts.Token);
                
                _logger.LogInformation("Cache {Status}", enabled ? "enabled" : "disabled");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Setting cache enabled state was cancelled");
            }
            catch (ObjectDisposedException)
            {
                _logger.LogInformation("Cannot set cache enabled state - service is disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache enabled state to {Enabled}", enabled);
            }
        }
        
        /// <inheritdoc/>
        /// <exception cref="Exception">Handles and logs any exceptions that occur during operation</exception>
        public async Task ClearCacheAsync()
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
                
            try
            {
                // Clear the entire LLM response cache
                _cacheService.RemoveByPrefix("llm:");
                
                // Reset the metrics
                _metricsService.Reset();
                
                // Save updated (empty) statistics
                await SaveStatisticsToConfigAsync(false, _cts.Token);
                
                _logger.LogInformation("Cache cleared");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Clearing cache was cancelled");
            }
            catch (ObjectDisposedException)
            {
                _logger.LogInformation("Cannot clear cache - service is disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }
        
        /// <inheritdoc/>
        public async Task SetCacheTypeAsync(string cacheType)
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
                
            try
            {
                if (string.IsNullOrEmpty(cacheType))
                {
                    throw new ArgumentException("Cache type cannot be empty");
                }
                
                var options = _cacheOptions.Value;
                var oldType = options.CacheType;
                
                // Validate cache type
                if (cacheType.ToLowerInvariant() != "memory" && cacheType.ToLowerInvariant() != "redis")
                {
                    throw new ArgumentException($"Invalid cache type: {cacheType}. Valid values are 'Memory' or 'Redis'");
                }
                
                // Check if service is disposed before expensive operation
                if (_isDisposed)
                    return;
                    
                // If changing type, clear cache first
                if (oldType?.ToLowerInvariant() != cacheType.ToLowerInvariant() && options.IsEnabled)
                {
                    await ClearCacheAsync();
                    
                    // Re-check if service is disposed after potentially expensive operation
                    if (_isDisposed)
                        return;
                }
                
                // Update options
                options.CacheType = cacheType;
                
                // Save configuration
                await SaveCacheConfigAsync(_cts.Token);
                
                _logger.LogInformation("Cache type set to {CacheType}", cacheType);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Setting cache type was cancelled");
            }
            catch (ObjectDisposedException)
            {
                _logger.LogInformation("Cannot set cache type - service is disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache type to {CacheType}", cacheType);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task UpdateRedisSettingsAsync(string connectionString, string instanceName)
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
                
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentException("Redis connection string cannot be empty");
                }
                
                var options = _cacheOptions.Value;
                
                // Test connection before saving
                if (_redisCacheMetrics != null)
                {
                    // Check if service is disposed before external call
                    if (_isDisposed)
                        return;
                        
                    var testResult = await _redisCacheMetrics.TestRedisConnectionAsync(connectionString);
                    if (!testResult.IsSuccess)
                    {
                        throw new Exception($"Failed to connect to Redis: {testResult.ErrorMessage}");
                    }
                    
                    // Re-check if service is disposed after external call
                    if (_isDisposed)
                        return;
                }
                
                // Update options
                options.RedisConnectionString = connectionString;
                options.RedisInstanceName = instanceName ?? "conduit:";
                
                // Save configuration
                await SaveCacheConfigAsync(_cts.Token);
                
                _logger.LogInformation("Redis settings updated");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Updating Redis settings was cancelled");
            }
            catch (ObjectDisposedException)
            {
                _logger.LogInformation("Cannot update Redis settings - service is disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Redis settings");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<RedisConnectionTestResult> TestRedisConnectionAsync(string connectionString)
        {
            // Check if service is disposed
            if (_isDisposed)
            {
                return new RedisConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Cache service is disposed"
                };
            }
            
            try
            {
                if (_redisCacheMetrics == null)
                {
                    return new RedisConnectionTestResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Redis metrics service is not available"
                    };
                }
                
                // Use the cancellation token to ensure we can cancel if needed
                return await _redisCacheMetrics.TestRedisConnectionAsync(connectionString);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Testing Redis connection was cancelled");
                return new RedisConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Connection test was cancelled"
                };
            }
            catch (ObjectDisposedException)
            {
                return new RedisConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Cache service was disposed during connection test"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Redis connection");
                return new RedisConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Exception testing connection: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// Initializes the cache from configuration stored in the database
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method retrieves cache configuration from the database and applies it to the current
        /// cache options. It also initializes the metrics service with previously saved statistics if available.
        /// If no configuration exists in the database, it creates a default configuration.
        /// </remarks>
        /// <exception cref="Exception">Handles and logs any exceptions that occur during initialization</exception>
        private async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
            
            bool lockAcquired = false;
            try
            {
                // Try to acquire the lock with cancellation support
                lockAcquired = await _configLock.WaitAsync(5000, cancellationToken);
                if (!lockAcquired)
                {
                    _logger.LogWarning("Failed to acquire config lock for initialization after timeout");
                    return;
                }
                
                // Double-check cancellation and disposal after lock acquisition
                if (cancellationToken.IsCancellationRequested || _isDisposed)
                    return;
                
                try
                {
                    // Get cache configuration from settings using repository
                    var cacheSetting = await _globalSettingRepository.GetByKeyAsync(CACHE_CONFIG_KEY);
                    
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
                                
                                // Check for cancellation and disposal again before potentially lengthy operation
                                if (cancellationToken.IsCancellationRequested || _isDisposed)
                                    return;
                                
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
                        // Check for cancellation and disposal before creating default settings
                        if (cancellationToken.IsCancellationRequested || _isDisposed)
                            return;
                            
                        // Create default cache settings
                        await SaveCacheConfigAsync(cancellationToken);
                        _logger.LogInformation("Default cache configuration created");
                    }
                }
                finally
                {
                    if (lockAcquired && !_isDisposed)
                    {
                        _configLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cache initialization was cancelled");
                // Rethrow cancellation
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing cache");
                // Only release if we acquired the lock and it hasn't been disposed
                if (lockAcquired && !_isDisposed && _configLock.CurrentCount == 0)
                {
                    try
                    {
                        _configLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore if already disposed
                    }
                }
            }
        }
        
        /// <summary>
        /// Saves the current cache configuration to the database
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method saves the current cache configuration options along with the latest statistics
        /// to the database for persistence. It uses a semaphore to ensure thread safety during database access.
        /// </remarks>
        /// <exception cref="Exception">Handles and logs any exceptions that occur during the save operation</exception>
        private async Task SaveCacheConfigAsync(CancellationToken cancellationToken = default)
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
                
            bool lockAcquired = false;
            try
            {
                // Try to acquire the lock with cancellation support
                lockAcquired = await _configLock.WaitAsync(5000, cancellationToken);
                if (!lockAcquired)
                {
                    _logger.LogWarning("Failed to acquire config lock for saving configuration after timeout");
                    return;
                }
                
                // Double-check cancellation and disposal after lock acquisition
                if (cancellationToken.IsCancellationRequested || _isDisposed)
                    return;
                
                try
                {
                    // Get the latest metrics before saving
                    await SaveStatisticsToConfigAsync(true, cancellationToken);
                }
                finally
                {
                    if (lockAcquired && !_isDisposed)
                    {
                        _configLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Saving cache configuration was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cache configuration");
                // Only release if we acquired the lock and it hasn't been disposed
                if (lockAcquired && !_isDisposed && _configLock.CurrentCount == 0)
                {
                    try 
                    {
                        _configLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore if already disposed
                    }
                }
            }
        }
        
        /// <summary>
        /// Saves the current cache statistics to the configuration in the database
        /// </summary>
        /// <param name="includeConfig">Whether to include configuration settings in the save operation</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method persists the current cache statistics to the database, and optionally also saves
        /// the current configuration settings. It captures both global statistics and model-specific statistics.
        /// This is called periodically by a timer to ensure statistics are not lost on application shutdown.
        /// </remarks>
        /// <exception cref="Exception">Handles and logs any exceptions that occur during the save operation</exception>
        private async Task SaveStatisticsToConfigAsync(bool includeConfig = false, CancellationToken cancellationToken = default)
        {
            // Check if service is disposed
            if (_isDisposed)
                return;
                
            bool lockAcquired = false;
            try
            {
                // Try to acquire the lock with cancellation support
                lockAcquired = await _configLock.WaitAsync(5000, cancellationToken);
                if (!lockAcquired)
                {
                    _logger.LogWarning("Failed to acquire config lock for saving statistics after timeout");
                    return;
                }
                
                // Double-check cancellation and disposal after lock acquisition
                if (cancellationToken.IsCancellationRequested || _isDisposed)
                    return;
                
                try
                {
                    // Get existing config or create new one using repository
                    var cacheSetting = await _globalSettingRepository.GetByKeyAsync(CACHE_CONFIG_KEY);
                    
                    // Check for cancellation after repository call
                    if (cancellationToken.IsCancellationRequested || _isDisposed)
                        return;
                    
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
                        
                        // Create a new global setting using repository
                        cacheSetting = new ConduitLLM.Configuration.Entities.GlobalSetting 
                        {
                            Key = CACHE_CONFIG_KEY,
                            Value = "{}",
                            Description = "Cache configuration and statistics"
                        };
                        
                        // Add the new setting
                        await _globalSettingRepository.CreateAsync(cacheSetting);
                        
                        // Check for cancellation after repository update
                        if (cancellationToken.IsCancellationRequested || _isDisposed)
                            return;
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
                    
                    // Final cancellation check before expensive or database operations
                    if (cancellationToken.IsCancellationRequested || _isDisposed)
                        return;
                    
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
                    
                    // Save the updated config using repository
                    cacheSetting.Value = JsonSerializer.Serialize(config);
                    await _globalSettingRepository.UpdateAsync(cacheSetting);
                    
                    // Update the cached config
                    _lastLoadedConfig = config;
                    
                    _logger.LogDebug("Cache statistics saved to database. Total requests: {TotalRequests}, Hit rate: {HitRate}%", 
                        config.TotalRequests, config.HitRate * 100);
                }
                finally
                {
                    if (lockAcquired && !_isDisposed)
                    {
                        _configLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Saving cache statistics was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cache statistics");
                // Only release if we acquired the lock and it hasn't been disposed
                if (lockAcquired && !_isDisposed && _configLock.CurrentCount == 0)
                {
                    try 
                    {
                        _configLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore if already disposed
                    }
                }
            }
        }
        
        /// <summary>
        /// Estimates the memory usage of the cache based on configuration and usage patterns
        /// </summary>
        /// <param name="options">The cache options containing configuration settings</param>
        /// <returns>Estimated memory usage in bytes</returns>
        /// <remarks>
        /// This method provides a rough estimate of cache memory usage based on:
        /// - The cache type (Memory or Redis)
        /// - The number of cached items
        /// - Estimated average size of cached responses
        /// 
        /// For in-memory caches, it includes a baseline overhead plus per-item estimates.
        /// For Redis caches, it provides a simpler estimate based on item count and average size.
        /// These are rough estimates and not precise measurements of actual memory usage.
        /// </remarks>
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
        /// <remarks>
        /// This method stops and disposes of the statistics timer and the semaphore lock.
        /// It ensures proper cleanup of resources when the service is being shut down.
        /// </remarks>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            try
            {
                // Signal cancellation to stop any pending operations
                _cts.Cancel();
                
                // Stop and dispose of the timer
                _statisticsTimer?.Dispose();
                
                // Dispose the cancellation token source
                _cts.Dispose();
                
                // Only dispose the semaphore after pending operations should be stopped
                _configLock.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CacheStatusService disposal");
            }
        }
        
        /// <summary>
        /// Configuration class for cache settings and statistics stored in the database
        /// </summary>
        /// <remarks>
        /// This class encapsulates both configuration settings and runtime statistics for the cache.
        /// It is serialized to JSON and stored in the global settings table for persistence between
        /// application restarts. It includes general cache settings, key generation options,
        /// model-specific rules, and both global and per-model statistics.
        /// </remarks>
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
        /// <remarks>
        /// This class represents a model-specific caching rule that determines whether a model's
        /// responses should be cached and for how long. These rules are stored in the database
        /// as part of the overall cache configuration and can be used to apply different caching
        /// policies to different models.
        /// </remarks>
        private class ModelRuleConfig
        {
            public string ModelNamePattern { get; set; } = string.Empty;
            public int CacheBehavior { get; set; } = 0;
            public int? ExpirationMinutes { get; set; }
        }
        
        /// <summary>
        /// Model-specific cache statistics for database storage
        /// </summary>
        /// <remarks>
        /// This class tracks cache performance metrics for individual models, including hits, misses,
        /// retrieval times, hit rates, and average retrieval times. These statistics are stored in the
        /// database as part of the overall cache statistics and can be used to analyze cache performance
        /// on a per-model basis.
        /// </remarks>
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
