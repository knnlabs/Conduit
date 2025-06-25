using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for tracking and analyzing image generation performance metrics.
    /// </summary>
    public class ImageGenerationMetricsService : IImageGenerationMetricsService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ImageGenerationMetricsService> _logger;
        private readonly ConcurrentDictionary<string, List<ImageGenerationMetrics>> _metricsStore;
        private readonly ConcurrentDictionary<string, int> _queueDepths;
        private readonly ConcurrentDictionary<string, DateTime> _unhealthyProviders;
        private readonly SemaphoreSlim _cleanupSemaphore;
        
        public ImageGenerationMetricsService(
            IMemoryCache cache,
            ILogger<ImageGenerationMetricsService> logger)
        {
            _cache = cache;
            _logger = logger;
            _metricsStore = new ConcurrentDictionary<string, List<ImageGenerationMetrics>>();
            _queueDepths = new ConcurrentDictionary<string, int>();
            _unhealthyProviders = new ConcurrentDictionary<string, DateTime>();
            _cleanupSemaphore = new SemaphoreSlim(1, 1);
        }
        
        public Task RecordMetricAsync(ImageGenerationMetrics metric, CancellationToken cancellationToken = default)
        {
            var key = GetProviderKey(metric.Provider, metric.Model);
            
            _metricsStore.AddOrUpdate(key,
                new List<ImageGenerationMetrics> { metric },
                (k, list) =>
                {
                    lock (list)
                    {
                        list.Add(metric);
                        // Keep only last 1000 metrics per provider to prevent memory growth
                        if (list.Count > 1000)
                        {
                            list.RemoveRange(0, list.Count - 1000);
                        }
                    }
                    return list;
                });
            
            // Invalidate cached stats for this provider
            var cacheKey = $"image_gen_stats_{key}";
            _cache.Remove(cacheKey);
            
            _logger.LogDebug("Recorded image generation metric for {Provider}/{Model}: {GenerationTime}ms, Success: {Success}",
                metric.Provider, metric.Model, metric.TotalGenerationTimeMs, metric.Success);
            
            return Task.CompletedTask;
        }
        
        public async Task<ImageGenerationProviderStats?> GetProviderStatsAsync(
            string provider, 
            string model, 
            int windowMinutes = 60,
            CancellationToken cancellationToken = default)
        {
            var key = GetProviderKey(provider, model);
            var cacheKey = $"image_gen_stats_{key}_{windowMinutes}";
            
            // Check cache first
            if (_cache.TryGetValue<ImageGenerationProviderStats>(cacheKey, out var cachedStats))
            {
                return cachedStats;
            }
            
            // Calculate stats
            var stats = await Task.Run(() => CalculateProviderStats(provider, model, windowMinutes), cancellationToken);
            
            if (stats != null)
            {
                // Cache for 1 minute
                _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(1));
            }
            
            return stats;
        }
        
        public async Task<IReadOnlyList<ImageGenerationProviderStats>> GetAllProviderStatsAsync(
            int windowMinutes = 60,
            CancellationToken cancellationToken = default)
        {
            var tasks = _metricsStore.Keys.Select(async key =>
            {
                var parts = key.Split('|');
                if (parts.Length == 2)
                {
                    return await GetProviderStatsAsync(parts[0], parts[1], windowMinutes, cancellationToken);
                }
                return null;
            });
            
            var results = await Task.WhenAll(tasks);
            return results.Where(s => s != null).Cast<ImageGenerationProviderStats>().ToList();
        }
        
        public async Task<(string Provider, string Model)?> SelectOptimalProviderAsync(
            IEnumerable<(string Provider, string Model)> availableProviders,
            int imageCount,
            double? maxWaitTimeSeconds = null,
            CancellationToken cancellationToken = default)
        {
            var providerStats = new List<(string Provider, string Model, ImageGenerationProviderStats Stats)>();
            
            foreach (var (provider, model) in availableProviders)
            {
                var stats = await GetProviderStatsAsync(provider, model, 60, cancellationToken);
                if (stats != null && stats.IsHealthy)
                {
                    providerStats.Add((provider, model, stats));
                }
            }
            
            if (!providerStats.Any())
            {
                // If no stats available, return the first available provider
                return availableProviders.FirstOrDefault();
            }
            
            // Filter by max wait time if specified
            if (maxWaitTimeSeconds.HasValue)
            {
                providerStats = providerStats
                    .Where(p => p.Stats.EstimatedWaitTimeSeconds <= maxWaitTimeSeconds.Value)
                    .ToList();
            }
            
            if (!providerStats.Any())
            {
                return null;
            }
            
            // Select provider with best combination of speed and reliability
            var selected = providerStats
                .OrderBy(p => p.Stats.EstimatedWaitTimeSeconds + (p.Stats.AvgGenerationTimeMs * imageCount / 1000))
                .ThenByDescending(p => p.Stats.SuccessRate)
                .ThenByDescending(p => p.Stats.HealthScore)
                .FirstOrDefault();
            
            return selected.Provider != null ? (selected.Provider, selected.Model) : null;
        }
        
        public Task UpdateQueueDepthAsync(
            string provider, 
            string model, 
            int queueDepth,
            CancellationToken cancellationToken = default)
        {
            var key = GetProviderKey(provider, model);
            _queueDepths.AddOrUpdate(key, queueDepth, (k, v) => queueDepth);
            
            // Invalidate cached stats
            var cacheKey = $"image_gen_stats_{key}";
            _cache.Remove(cacheKey);
            
            return Task.CompletedTask;
        }
        
        public Task MarkProviderUnhealthyAsync(
            string provider, 
            string model, 
            string reason,
            CancellationToken cancellationToken = default)
        {
            var key = GetProviderKey(provider, model);
            _unhealthyProviders.AddOrUpdate(key, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
            
            _logger.LogWarning("Marked provider {Provider}/{Model} as unhealthy: {Reason}",
                provider, model, reason);
            
            // Invalidate cached stats
            var cacheKey = $"image_gen_stats_{key}";
            _cache.Remove(cacheKey);
            
            return Task.CompletedTask;
        }
        
        public async Task<int> CleanupOldMetricsAsync(
            int olderThanDays = 7,
            CancellationToken cancellationToken = default)
        {
            await _cleanupSemaphore.WaitAsync(cancellationToken);
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var totalRemoved = 0;
                
                foreach (var kvp in _metricsStore)
                {
                    if (kvp.Value != null)
                    {
                        lock (kvp.Value)
                        {
                            var initialCount = kvp.Value.Count;
                            kvp.Value.RemoveAll(m => m.StartedAt < cutoffDate);
                            totalRemoved += initialCount - kvp.Value.Count;
                        }
                    }
                }
                
                // Clean up unhealthy provider entries older than 1 hour
                var unhealthyCutoff = DateTime.UtcNow.AddHours(-1);
                var keysToRemove = _unhealthyProviders
                    .Where(kvp => kvp.Value < unhealthyCutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in keysToRemove)
                {
                    _unhealthyProviders.TryRemove(key, out _);
                }
                
                _logger.LogInformation("Cleaned up {Count} old image generation metrics", totalRemoved);
                
                return totalRemoved;
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }
        
        private ImageGenerationProviderStats? CalculateProviderStats(
            string provider, 
            string model, 
            int windowMinutes)
        {
            var key = GetProviderKey(provider, model);
            
            if (!_metricsStore.TryGetValue(key, out var metricsList))
            {
                return null;
            }
            
            var cutoffTime = DateTime.UtcNow.AddMinutes(-windowMinutes);
            List<ImageGenerationMetrics> recentMetrics;
            
            lock (metricsList)
            {
                recentMetrics = metricsList
                    .Where(m => m.StartedAt >= cutoffTime)
                    .ToList();
            }
            
            if (!recentMetrics.Any())
            {
                return null;
            }
            
            var successfulMetrics = recentMetrics.Where(m => m.Success).ToList();
            var generationTimes = successfulMetrics
                .Select(m => (double)m.TotalGenerationTimeMs)
                .OrderBy(t => t)
                .ToList();
            
            // Check if provider is marked as unhealthy
            var isHealthy = !_unhealthyProviders.ContainsKey(key);
            
            // Get current queue depth
            _queueDepths.TryGetValue(key, out var queueDepth);
            
            var stats = new ImageGenerationProviderStats
            {
                Provider = provider,
                Model = model,
                RequestCount = recentMetrics.Count,
                SuccessRate = recentMetrics.Count > 0 ? (double)successfulMetrics.Count / recentMetrics.Count : 0,
                WindowMinutes = windowMinutes,
                CurrentQueueDepth = queueDepth,
                IsHealthy = isHealthy
            };
            
            if (generationTimes.Any())
            {
                stats.AvgGenerationTimeMs = generationTimes.Average();
                stats.P95GenerationTimeMs = GetPercentile(generationTimes, 0.95);
                
                // Estimate wait time based on queue depth and average generation time
                var avgTimePerImage = successfulMetrics.Any() 
                    ? successfulMetrics.Average(m => m.AvgGenerationTimePerImageMs) 
                    : stats.AvgGenerationTimeMs;
                    
                stats.EstimatedWaitTimeSeconds = (queueDepth * avgTimePerImage) / 1000.0;
            }
            
            // Calculate health score (0.0 to 1.0)
            stats.HealthScore = CalculateHealthScore(stats);
            
            return stats;
        }
        
        private double CalculateHealthScore(ImageGenerationProviderStats stats)
        {
            if (!stats.IsHealthy)
            {
                return 0.0;
            }
            
            var score = 1.0;
            
            // Penalize for low success rate
            score *= stats.SuccessRate;
            
            // Penalize for high queue depth
            if (stats.CurrentQueueDepth > 10)
            {
                score *= 10.0 / stats.CurrentQueueDepth;
            }
            
            // Penalize for slow generation times (baseline: 30 seconds)
            if (stats.AvgGenerationTimeMs > 30000)
            {
                score *= 30000.0 / stats.AvgGenerationTimeMs;
            }
            
            // Penalize for low request volume (might indicate issues)
            if (stats.RequestCount < 5 && stats.WindowMinutes >= 60)
            {
                score *= 0.8;
            }
            
            return Math.Max(0.0, Math.Min(1.0, score));
        }
        
        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any())
            {
                return 0;
            }
            
            if (sortedValues.Count == 1)
            {
                return sortedValues[0];
            }
            
            var index = percentile * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);
            var weight = index - lower;
            
            return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        }
        
        private string GetProviderKey(string provider, string model)
        {
            return $"{provider}|{model}";
        }
    }
}