using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Statistics and monitoring methods for CacheManagementService
    /// </summary>
    public partial class CacheManagementService
    {
        /// <summary>
        /// Gets statistics for all cache regions or a specific region.
        /// </summary>
        public async Task<CacheStatisticsDto> GetStatisticsAsync(string? regionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(regionId))
                {
                    return await GetOverallStatisticsAsync(cancellationToken);
                }

                if (Enum.TryParse<CacheRegion>(regionId, true, out var region))
                {
                    var stats = await _cacheManager.GetRegionStatisticsAsync(region, cancellationToken);
                    return ConvertToStatisticsDto(stats);
                }

                throw new ArgumentException($"Invalid region ID: {regionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache statistics");
                throw;
            }
        }

        /// <summary>
        /// Gets overall statistics across all cache regions
        /// </summary>
        private async Task<CacheStatisticsDto> GetOverallStatisticsAsync(CancellationToken cancellationToken)
        {
            var allStats = await _cacheManager.GetAllStatisticsAsync(cancellationToken);
            
            var totalHits = allStats.Sum(s => s.Value.HitCount);
            var totalMisses = allStats.Sum(s => s.Value.MissCount);
            var totalRequests = totalHits + totalMisses;
            var overallHitRate = totalRequests > 0 ? (double)totalHits / totalRequests * 100 : 0;

            var avgGetTime = allStats.Where(s => s.Value.AverageGetTime.TotalMilliseconds > 0)
                .Select(s => s.Value.AverageGetTime.TotalMilliseconds)
                .DefaultIfEmpty(0)
                .Average();

            var avgSetTime = allStats.Where(s => s.Value.AverageSetTime.TotalMilliseconds > 0)
                .Select(s => s.Value.AverageSetTime.TotalMilliseconds)
                .DefaultIfEmpty(0)
                .Average();

            return new CacheStatisticsDto
            {
                TotalHits = totalHits,
                TotalMisses = totalMisses,
                HitRate = overallHitRate,
                AvgResponseTime = new ResponseTimeDto
                {
                    WithCache = (int)avgGetTime,
                    WithoutCache = (int)(avgGetTime * 20) // Estimate based on typical cache benefit
                },
                MemoryUsage = new MemoryUsageDto
                {
                    Current = FormatSize(allStats.Sum(s => s.Value.TotalSizeBytes)),
                    Peak = FormatSize((long)(allStats.Sum(s => s.Value.TotalSizeBytes) * 1.5)), // Estimate
                    Limit = "1 GB"
                },
                TopCachedItems = await GetTopCachedItemsAsync(cancellationToken)
            };
        }

        /// <summary>
        /// Gets top cached items across regions
        /// </summary>
        private async Task<List<TopCachedItemDto>> GetTopCachedItemsAsync(CancellationToken cancellationToken)
        {
            // This would need a more sophisticated implementation to track individual key statistics
            // For now, return sample data based on regions
            var topItems = new List<TopCachedItemDto>();

            foreach (var region in new[] { CacheRegion.VirtualKeys, CacheRegion.ModelMetadata, CacheRegion.ProviderResponses })
            {
                var stats = await _cacheManager.GetRegionStatisticsAsync(region, cancellationToken);
                if (stats.HitCount > 0)
                {
                    topItems.Add(new TopCachedItemDto
                    {
                        Key = $"{region.ToString().ToLower()}:*",
                        Hits = stats.HitCount,
                        Size = FormatSize(stats.TotalSizeBytes / Math.Max(stats.EntryCount, 1))
                    });
                }
            }

            return topItems.OrderByDescending(i => i.Hits).Take(10).ToList();
        }

        /// <summary>
        /// Converts cache region statistics to DTO format
        /// </summary>
        private CacheStatisticsDto ConvertToStatisticsDto(CacheRegionStatistics stats)
        {
            return new CacheStatisticsDto
            {
                TotalHits = stats.HitCount,
                TotalMisses = stats.MissCount,
                HitRate = stats.HitRate * 100,
                AvgResponseTime = new ResponseTimeDto
                {
                    WithCache = (int)stats.AverageGetTime.TotalMilliseconds,
                    WithoutCache = (int)(stats.AverageGetTime.TotalMilliseconds * 20)
                },
                MemoryUsage = new MemoryUsageDto
                {
                    Current = FormatSize(stats.TotalSizeBytes),
                    Peak = FormatSize((long)(stats.TotalSizeBytes * 1.5)),
                    Limit = "N/A"
                }
            };
        }
    }
}