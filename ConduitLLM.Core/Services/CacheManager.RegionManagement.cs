using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Cache manager implementation - Region Management functionality
    /// </summary>
    public partial class CacheManager
    {
        public async Task<int> RemoveManyAsync(IEnumerable<string> keys, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var tasks = keys.Select(key => RemoveAsync(key, region, cancellationToken));
            var results = await Task.WhenAll(tasks);
            return results.Count(r => r);
        }

        public async Task<int> RemoveByPatternAsync(string pattern, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            // This is a simplified implementation. In production, we'd need more sophisticated pattern matching
            var keys = await ListKeysAsync(region, pattern, int.MaxValue, cancellationToken);
            return await RemoveManyAsync(keys, region, cancellationToken);
        }

        /// <summary>
        /// Clears all cache entries for the specified region.
        /// </summary>
        public async Task ClearRegionAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var clearedCount = 0;
                
                // Clear from memory cache using tracked keys
                if (_regionKeys.TryGetValue(region, out var regionKeys))
                {
                    var keysList = regionKeys.Keys.ToList();
                    foreach (var key in keysList)
                    {
                        var fullKey = BuildKey(key, region);
                        _memoryCache.Remove(fullKey);
                        regionKeys.TryRemove(key, out _);
                        clearedCount++;
                    }
                }

                // Clear from distributed cache if available
                if (_useDistributedCache && _distributedCache != null)
                {
                    // TODO: Implement distributed cache clearing based on specific cache provider
                    _logger.LogWarning("Distributed cache clear not fully implemented for region {Region}", region);
                }

                _logger.LogInformation("Cleared {Count} entries from cache region {Region}", clearedCount, region);
                await UpdateStatisticsAsync(region, "Clear", stopwatch.Elapsed, true);

                // Reset statistics for the region
                _statistics[region] = new CacheRegionStatistics { Region = region, LastResetTime = DateTime.UtcNow };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache region {Region}", region);
                await UpdateStatisticsAsync(region, "ClearError", stopwatch.Elapsed, false);
                throw;
            }
        }

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            var tasks = Enum.GetValues<CacheRegion>()
                .Select(region => ClearRegionAsync(region, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public async Task<bool> ExistsAsync(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var value = await GetAsync<object>(key, region, cancellationToken);
            return value != null;
        }

        public CacheRegionConfig GetRegionConfig(CacheRegion region)
        {
            return _regionConfigs.GetOrAdd(region, CreateDefaultConfig);
        }

        public Task UpdateRegionConfigAsync(CacheRegionConfig config)
        {
            _regionConfigs[config.Region] = config;
            _logger.LogInformation("Updated configuration for cache region {Region}", config.Region);
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<string>> ListKeysAsync(CacheRegion region, string? pattern = null, int maxCount = 100, CancellationToken cancellationToken = default)
        {
            if (!_regionKeys.TryGetValue(region, out var regionKeys))
            {
                return await Task.FromResult(Enumerable.Empty<string>());
            }

            var keys = regionKeys.Keys.AsEnumerable();

            // Apply pattern filtering if provided
            if (!string.IsNullOrEmpty(pattern))
            {
                // Simple wildcard pattern matching (supports * and ?)
                var regex = new System.Text.RegularExpressions.Regex(
                    "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                keys = keys.Where(k => regex.IsMatch(k));
            }

            return await Task.FromResult(keys.Take(maxCount).ToList());
        }

        public async Task<IEnumerable<CacheEntry<object>>> GetEntriesAsync(CacheRegion region, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            // This is a simplified implementation
            _logger.LogWarning("GetEntriesAsync is not fully implemented - returning empty list");
            return await Task.FromResult(Enumerable.Empty<CacheEntry<object>>());
        }

        public async Task<bool> RefreshAsync(string key, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var value = await GetAsync<object>(key, region, cancellationToken);
            if (value == null)
                return false;

            await SetAsync(key, value, region, ttl, cancellationToken);
            return true;
        }
    }
}