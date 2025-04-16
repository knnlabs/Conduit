using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConduitLLM.Core.Caching
{
    /// <summary>
    /// Represents cache metrics for a specific model
    /// </summary>
    public class ModelCacheMetrics
    {
        /// <summary>
        /// Number of cache hits for this model
        /// </summary>
        public long Hits { get; set; }
        
        /// <summary>
        /// Number of cache misses for this model
        /// </summary>
        public long Misses { get; set; }
        
        /// <summary>
        /// Total retrieval time in milliseconds for this model
        /// </summary>
        public long TotalRetrievalTimeMs { get; set; }
        
        /// <summary>
        /// Gets the cache hit rate (hits / total requests)
        /// </summary>
        /// <returns>Hit rate as a value between 0 and 1</returns>
        public double GetHitRate()
        {
            long total = Hits + Misses;
            
            if (total == 0)
                return 0;
                
            return (double)Hits / total;
        }
        
        /// <summary>
        /// Gets the average retrieval time in milliseconds
        /// </summary>
        /// <returns>Average retrieval time</returns>
        public double GetAverageRetrievalTimeMs()
        {
            if (Hits == 0)
                return 0;
                
            return (double)TotalRetrievalTimeMs / Hits;
        }
    }

    /// <summary>
    /// Service that tracks cache metrics
    /// </summary>
    public class CacheMetricsService : ICacheMetricsService
    {
        private long _hits;
        private long _misses;
        private long _totalRetrievalTimeMs;
        private readonly ILogger<CacheMetricsService> _logger;
        private readonly ConcurrentDictionary<string, ModelCacheMetricsInternal> _modelMetrics = new();
        
        /// <summary>
        /// Creates a new instance of the CacheMetricsService
        /// </summary>
        /// <param name="logger">Logger</param>
        public CacheMetricsService(ILogger<CacheMetricsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hits = 0;
            _misses = 0;
            _totalRetrievalTimeMs = 0;
        }
        
        /// <inheritdoc/>
        public void RecordHit(double retrievalTimeMs, string? model = null)
        {
            Interlocked.Increment(ref _hits);
            Interlocked.Add(ref _totalRetrievalTimeMs, (long)retrievalTimeMs);
            
            // Record model-specific metrics if a model was provided
            if (!string.IsNullOrEmpty(model))
            {
                var metrics = _modelMetrics.GetOrAdd(model, _ => new ModelCacheMetricsInternal());
                
                Interlocked.Increment(ref metrics._hits);
                Interlocked.Add(ref metrics._totalRetrievalTimeMs, (long)retrievalTimeMs);
            }
            
            if (_hits % 100 == 0)
            {
                _logger.LogDebug("Cache hit #{HitCount}, avg retrieval time: {AvgTime:F2}ms, hit rate: {HitRate:P2}", 
                    _hits, GetAverageRetrievalTimeMs(), GetHitRate());
            }
        }
        
        /// <inheritdoc/>
        public void RecordMiss(string? model = null)
        {
            Interlocked.Increment(ref _misses);
            
            // Record model-specific metrics if a model was provided
            if (!string.IsNullOrEmpty(model))
            {
                var metrics = _modelMetrics.GetOrAdd(model, _ => new ModelCacheMetricsInternal());
                Interlocked.Increment(ref metrics._misses);
            }
            
            if (_misses % 100 == 0)
            {
                _logger.LogDebug("Cache miss #{MissCount}, hit rate: {HitRate:P2}", _misses, GetHitRate());
            }
        }
        
        /// <inheritdoc/>
        public long GetTotalHits()
        {
            return Interlocked.Read(ref _hits);
        }
        
        /// <inheritdoc/>
        public long GetTotalMisses()
        {
            return Interlocked.Read(ref _misses);
        }
        
        /// <inheritdoc/>
        public long GetTotalRequests()
        {
            return Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
        }
        
        /// <inheritdoc/>
        public double GetHitRate()
        {
            long hits = Interlocked.Read(ref _hits);
            long total = hits + Interlocked.Read(ref _misses);
            
            if (total == 0)
                return 0;
                
            return (double)hits / total;
        }
        
        /// <inheritdoc/>
        public double GetAverageRetrievalTimeMs()
        {
            long hits = Interlocked.Read(ref _hits);
            long totalTime = Interlocked.Read(ref _totalRetrievalTimeMs);
            
            if (hits == 0)
                return 0;
                
            return (double)totalTime / hits;
        }
        
        /// <inheritdoc/>
        public IDictionary<string, ModelCacheMetrics> GetModelMetrics()
        {
            // Return a deep copy of the model metrics to prevent concurrent modification issues
            var result = new Dictionary<string, ModelCacheMetrics>();
            
            foreach (var kvp in _modelMetrics)
            {
                var metrics = new ModelCacheMetrics
                {
                    Hits = Interlocked.Read(ref kvp.Value._hits),
                    Misses = Interlocked.Read(ref kvp.Value._misses),
                    TotalRetrievalTimeMs = Interlocked.Read(ref kvp.Value._totalRetrievalTimeMs)
                };
                
                result.Add(kvp.Key, metrics);
            }
            
            return result;
        }
        
        /// <inheritdoc/>
        public ModelCacheMetrics? GetMetricsForModel(string model)
        {
            if (string.IsNullOrEmpty(model) || !_modelMetrics.TryGetValue(model, out var metrics))
            {
                return null;
            }
            
            // Return a copy to prevent concurrent modification issues
            return new ModelCacheMetrics
            {
                Hits = Interlocked.Read(ref metrics._hits),
                Misses = Interlocked.Read(ref metrics._misses),
                TotalRetrievalTimeMs = Interlocked.Read(ref metrics._totalRetrievalTimeMs)
            };
        }
        
        /// <inheritdoc/>
        public IList<string> GetTrackedModels()
        {
            return _modelMetrics.Keys.ToList();
        }
        
        /// <inheritdoc/>
        public void Reset()
        {
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
            Interlocked.Exchange(ref _totalRetrievalTimeMs, 0);
            
            // Clear model-specific metrics
            _modelMetrics.Clear();
            
            _logger.LogInformation("Cache metrics reset");
        }
        
        /// <inheritdoc/>
        public void ImportStats(long hits, long misses, double avgResponseTimeMs, 
            IDictionary<string, ModelCacheMetrics>? modelMetrics = null)
        {
            if (hits < 0 || misses < 0 || avgResponseTimeMs < 0)
            {
                _logger.LogWarning("Attempted to import invalid cache statistics. Hits: {Hits}, Misses: {Misses}, AvgResponse: {AvgResponse}ms",
                    hits, misses, avgResponseTimeMs);
                return;
            }
            
            // Only import stats if we don't have any data yet
            if (GetTotalRequests() == 0)
            {
                Interlocked.Exchange(ref _hits, hits);
                Interlocked.Exchange(ref _misses, misses);
                
                // Calculate total retrieval time based on imported average
                long totalTime = (long)(hits * avgResponseTimeMs);
                Interlocked.Exchange(ref _totalRetrievalTimeMs, totalTime);
                
                // Import model-specific metrics if provided
                if (modelMetrics != null && modelMetrics.Count > 0)
                {
                    foreach (var kvp in modelMetrics)
                    {
                        if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                        {
                            var internalMetrics = new ModelCacheMetricsInternal();
                            Interlocked.Exchange(ref internalMetrics._hits, kvp.Value.Hits);
                            Interlocked.Exchange(ref internalMetrics._misses, kvp.Value.Misses);
                            Interlocked.Exchange(ref internalMetrics._totalRetrievalTimeMs, kvp.Value.TotalRetrievalTimeMs);
                            
                            _modelMetrics[kvp.Key] = internalMetrics;
                        }
                    }
                }
                
                _logger.LogInformation("Imported cache statistics: {Hits} hits, {Misses} misses, {AvgTime:F2}ms average response time, {ModelCount} models",
                    hits, misses, avgResponseTimeMs, modelMetrics?.Count ?? 0);
            }
            else
            {
                _logger.LogInformation("Cache metrics already have data, skipping import");
            }
        }
        
        /// <summary>
        /// Internal implementation of model cache metrics with fields for thread-safe operations
        /// </summary>
        private class ModelCacheMetricsInternal
        {
            public long _hits;
            public long _misses;
            public long _totalRetrievalTimeMs;
        }
    }
}
