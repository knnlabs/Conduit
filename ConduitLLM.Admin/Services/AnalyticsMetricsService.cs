using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Interfaces;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for collecting and reporting analytics metrics
    /// </summary>
    public class AnalyticsMetricsService : IAnalyticsMetrics
    {
        private readonly ILogger<AnalyticsMetricsService> _logger;
        private readonly ConcurrentDictionary<string, long> _cacheHits = new();
        private readonly ConcurrentDictionary<string, long> _cacheMisses = new();
        private readonly ConcurrentDictionary<string, List<double>> _operationDurations = new();
        private readonly ConcurrentDictionary<string, List<double>> _fetchDurations = new();
        private long _cacheMemoryUsage;
        private long _totalCacheInvalidations;
        private DateTime _metricsStartTime;

        /// <summary>
        /// Initializes a new instance of the AnalyticsMetricsService
        /// </summary>
        /// <param name="logger">Logger instance for metric events</param>
        public AnalyticsMetricsService(ILogger<AnalyticsMetricsService> logger)
        {
            _logger = logger;
            _metricsStartTime = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public void RecordCacheHit(string cacheKey)
        {
            var key = NormalizeCacheKey(cacheKey);
            _cacheHits.AddOrUpdate(key, 1, (k, v) => v + 1);
            
            // Log every 100 hits for monitoring
            if (_cacheHits[key] % 100 == 0)
            {
                _logger.LogDebug("Cache key {CacheKey} has {HitCount} hits", key, _cacheHits[key]);
            }
        }

        /// <inheritdoc/>
        public void RecordCacheMiss(string cacheKey)
        {
            var key = NormalizeCacheKey(cacheKey);
            _cacheMisses.AddOrUpdate(key, 1, (k, v) => v + 1);
        }

        /// <inheritdoc/>
        public void RecordOperationDuration(string operationName, double durationMs)
        {
            _operationDurations.AddOrUpdate(
                operationName,
                new List<double> { durationMs },
                (k, list) =>
                {
                    list.Add(durationMs);
                    // Keep only last 1000 measurements to prevent memory growth
                    if (list.Count > 1000)
                    {
                        list.RemoveAt(0);
                    }
                    return list;
                });

            // Log slow operations
            if (durationMs > 1000)
            {
                _logger.LogWarning("Slow operation {OperationName} took {Duration}ms", 
                    operationName, durationMs);
            }
        }

        /// <inheritdoc/>
        public void RecordFetchDuration(string dataSource, double durationMs)
        {
            _fetchDurations.AddOrUpdate(
                dataSource,
                new List<double> { durationMs },
                (k, list) =>
                {
                    list.Add(durationMs);
                    // Keep only last 1000 measurements
                    if (list.Count > 1000)
                    {
                        list.RemoveAt(0);
                    }
                    return list;
                });
        }

        /// <inheritdoc/>
        public void RecordCacheMemoryUsage(long sizeBytes)
        {
            Interlocked.Exchange(ref _cacheMemoryUsage, sizeBytes);
            
            // Log if memory usage is high (> 100MB)
            if (sizeBytes > 100 * 1024 * 1024)
            {
                _logger.LogWarning("High cache memory usage: {SizeMB}MB", 
                    sizeBytes / (1024 * 1024));
            }
        }

        /// <inheritdoc/>
        public void RecordCacheInvalidation(string reason, int keysInvalidated)
        {
            Interlocked.Increment(ref _totalCacheInvalidations);
            _logger.LogInformation("Cache invalidated: {Reason}, {KeyCount} keys cleared", 
                reason, keysInvalidated);
        }

        /// <inheritdoc/>
        public Dictionary<string, object> GetCacheStatistics()
        {
            var totalHits = _cacheHits.Sum(kvp => kvp.Value);
            var totalMisses = _cacheMisses.Sum(kvp => kvp.Value);
            var hitRate = totalHits + totalMisses > 0 
                ? (double)totalHits / (totalHits + totalMisses) * 100 
                : 0;

            var stats = new Dictionary<string, object>
            {
                ["TotalHits"] = totalHits,
                ["TotalMisses"] = totalMisses,
                ["HitRate"] = Math.Round(hitRate, 2),
                ["CacheMemoryMB"] = _cacheMemoryUsage / (1024.0 * 1024.0),
                ["TotalInvalidations"] = _totalCacheInvalidations,
                ["UptimeMinutes"] = (DateTime.UtcNow - _metricsStartTime).TotalMinutes,
                ["TopHitKeys"] = GetTopKeys(_cacheHits, 5),
                ["TopMissKeys"] = GetTopKeys(_cacheMisses, 5)
            };

            return stats;
        }

        /// <inheritdoc/>
        public Dictionary<string, double> GetOperationStatistics()
        {
            var stats = new Dictionary<string, double>();

            foreach (var kvp in _operationDurations)
            {
                if (kvp.Value.Any())
                {
                    stats[$"{kvp.Key}_avg_ms"] = Math.Round(kvp.Value.Average(), 2);
                    stats[$"{kvp.Key}_p95_ms"] = Math.Round(GetPercentile(kvp.Value, 95), 2);
                    stats[$"{kvp.Key}_max_ms"] = Math.Round(kvp.Value.Max(), 2);
                }
            }

            foreach (var kvp in _fetchDurations)
            {
                if (kvp.Value.Any())
                {
                    stats[$"fetch_{kvp.Key}_avg_ms"] = Math.Round(kvp.Value.Average(), 2);
                    stats[$"fetch_{kvp.Key}_p95_ms"] = Math.Round(GetPercentile(kvp.Value, 95), 2);
                }
            }

            return stats;
        }

        private string NormalizeCacheKey(string cacheKey)
        {
            // Extract the key type (before the first colon)
            var colonIndex = cacheKey.IndexOf(':');
            if (colonIndex > 0)
            {
                return cacheKey.Substring(0, colonIndex);
            }
            return cacheKey;
        }

        private List<KeyValuePair<string, long>> GetTopKeys(
            ConcurrentDictionary<string, long> dictionary, int count)
        {
            return dictionary
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .ToList();
        }

        private double GetPercentile(List<double> values, int percentile)
        {
            if (!values.Any()) return 0;
            
            var sorted = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            index = Math.Max(0, Math.Min(index, sorted.Count - 1));
            
            return sorted[index];
        }
    }
}