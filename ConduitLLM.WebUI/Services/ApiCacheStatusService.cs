using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Caching;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service that provides cache status information through the Admin API
    /// </summary>
    /// <remarks>
    /// This implementation uses the IGlobalSettingService instead of direct database access,
    /// making it compatible with the Admin API mode in WebUI.
    /// </remarks>
    public class ApiCacheStatusService : ICacheStatusService, IDisposable
    {
        private readonly IGlobalSettingService _globalSettingService;
        private readonly ILogger<CacheStatusService> _logger;
        private readonly ICacheMetricsService _metricsService;
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly IRedisCacheMetricsService? _redisCacheMetrics;
        
        private readonly System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();
        private bool _isDisposed;

        /// <summary>
        /// Creates a new instance of the ApiCacheStatusService
        /// </summary>
        public ApiCacheStatusService(
            IGlobalSettingService globalSettingService,
            ICacheMetricsService metricsService,
            IOptions<CacheOptions> cacheOptions,
            ILogger<CacheStatusService> logger,
            IRedisCacheMetricsService? redisCacheMetrics = null)
        {
            _globalSettingService = globalSettingService ?? throw new ArgumentNullException(nameof(globalSettingService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redisCacheMetrics = redisCacheMetrics;
            
            _logger.LogInformation("ApiCacheStatusService initialized in Admin API mode");
        }
        
        /// <inheritdoc/>
        public async Task<CacheStatus> GetCacheStatusAsync()
        {
            // Check if service is disposed
            if (_isDisposed)
            {
                return new CacheStatus
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
                var result = new CacheStatus
                {
                    IsEnabled = options.IsEnabled,
                    CacheType = options.CacheType
                };
                
                // Get current metrics
                var totalRequests = _metricsService.GetTotalRequests();
                var hitRate = _metricsService.GetHitRate();
                var avgResponseTime = _metricsService.GetAverageRetrievalTimeMs();
                
                // Use current metrics
                result.TotalItems = (int)totalRequests;
                result.HitRate = hitRate;
                result.MemoryUsageBytes = EstimateMemoryUsage(options);
                result.AvgResponseTime = avgResponseTime;
                
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
                            result.RedisConnection = new RedisConnectionInfo
                            {
                                ConnectedClients = clientInfo.ConnectedClients,
                                Endpoint = serverInfo.TryGetValue("server:redis_version", out var version) ? 
                                    serverInfo.TryGetValue("server:os", out var os) ? $"{os} (Redis {version})" : $"Redis {version}" : 
                                    "Redis Server",
                                Version = serverInfo.TryGetValue("server:redis_version", out var v) ? v : "Unknown",
                                InstanceName = options.RedisInstanceName
                            };
                            
                            // Set Redis memory info
                            result.RedisMemory = new RedisMemoryInfo
                            {
                                UsedMemory = memoryStats.UsedMemory,
                                PeakMemory = memoryStats.PeakMemory,
                                FragmentationRatio = memoryStats.FragmentationRatio,
                                CachedMemory = memoryStats.CachedMemory
                            };
                            
                            // Set Redis database info
                            result.RedisDatabase = new RedisDatabaseInfo
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
                return new CacheStatus
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
                
                return new CacheStatus
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
        public Task SetCacheEnabledAsync(bool enabled)
        {
            // Check if service is disposed
            if (_isDisposed)
                return Task.CompletedTask;
                
            try
            {
                // Update options (but don't persist - that would be done through Admin API)
                var options = _cacheOptions.Value;
                options.IsEnabled = enabled;
                
                _logger.LogInformation("Cache {Status}", enabled ? "enabled" : "disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache enabled state to {Enabled}", enabled);
            }
            
            return Task.CompletedTask;
        }
        
        /// <inheritdoc/>
        public Task ClearCacheAsync()
        {
            // This implementation doesn't have direct access to clear the cache
            _logger.LogWarning("Cache clear operation not implemented in ApiCacheStatusService");
            return Task.CompletedTask;
        }
        
        /// <inheritdoc/>
        public Task SetCacheTypeAsync(string cacheType)
        {
            // Check if service is disposed
            if (_isDisposed)
                return Task.CompletedTask;
                
            try
            {
                if (string.IsNullOrEmpty(cacheType))
                {
                    throw new ArgumentException("Cache type cannot be empty");
                }
                
                var options = _cacheOptions.Value;
                
                // Validate cache type
                if (cacheType.ToLowerInvariant() != "memory" && cacheType.ToLowerInvariant() != "redis")
                {
                    throw new ArgumentException($"Invalid cache type: {cacheType}. Valid values are 'Memory' or 'Redis'");
                }
                
                // Update options (but don't persist - that would be done through Admin API)
                options.CacheType = cacheType;
                
                _logger.LogInformation("Cache type set to {CacheType}", cacheType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache type to {CacheType}", cacheType);
                throw;
            }
            
            return Task.CompletedTask;
        }
        
        /// <inheritdoc/>
        public Task UpdateRedisSettingsAsync(string connectionString, string instanceName)
        {
            // Check if service is disposed
            if (_isDisposed)
                return Task.CompletedTask;
                
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentException("Redis connection string cannot be empty");
                }
                
                var options = _cacheOptions.Value;
                
                // Update options (but don't persist - that would be done through Admin API)
                options.RedisConnectionString = connectionString;
                options.RedisInstanceName = instanceName ?? "conduit:";
                
                _logger.LogInformation("Redis settings updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Redis settings");
                throw;
            }
            
            return Task.CompletedTask;
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
        /// Estimates the memory usage of the cache based on configuration and usage patterns
        /// </summary>
        private long EstimateMemoryUsage(CacheOptions options)
        {
            if (!options.IsEnabled)
                return 0;
                
            if (options.CacheType == "Redis")
            {
                // For Redis, we don't have direct memory usage info
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
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            try
            {
                // Signal cancellation to stop any pending operations
                _cts.Cancel();
                
                // Dispose the cancellation token source
                _cts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ApiCacheStatusService disposal");
            }
        }
    }
}