using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Cache operations methods for CacheManagementService
    /// </summary>
    public partial class CacheManagementService
    {
        /// <summary>
        /// Clears a specific cache region or all caches.
        /// </summary>
        public async Task ClearCacheAsync(string cacheId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Clearing cache: {CacheId}", cacheId);

                if (cacheId.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    await _cacheManager.ClearAllAsync(cancellationToken);
                }
                else if (Enum.TryParse<CacheRegion>(cacheId, true, out var region))
                {
                    await _cacheManager.ClearRegionAsync(region, cancellationToken);
                }
                else
                {
                    throw new ArgumentException($"Invalid cache ID: {cacheId}");
                }

                // Log the operation
                _logger.LogInformation("Successfully cleared cache: {CacheId}", cacheId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache {CacheId}", cacheId);
                throw;
            }
        }

        /// <summary>
        /// Gets entries from a specific cache region with pagination.
        /// </summary>
        public async Task<CacheEntriesDto> GetEntriesAsync(string regionId, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Enum.TryParse<CacheRegion>(regionId, true, out var region))
                {
                    throw new ArgumentException($"Invalid region ID: {regionId}");
                }

                // Security: Only allow browsing of non-sensitive regions
                var sensitiveRegions = new[] { CacheRegion.AuthTokens, CacheRegion.Providers };
                if (sensitiveRegions.Contains(region))
                {
                    _logger.LogWarning("Attempted to browse sensitive cache region: {Region}", region);
                    return new CacheEntriesDto
                    {
                        RegionId = regionId,
                        Entries = new List<CacheEntryDto>(),
                        TotalCount = 0,
                        Message = "Access to this cache region is restricted for security reasons"
                    };
                }

                var entries = await _cacheManager.GetEntriesAsync(region, skip, take, cancellationToken);
                var entryDtos = entries.Select(e => new CacheEntryDto
                {
                    Key = e.Key,
                    Size = FormatSize(e.SizeInBytes ?? 0),
                    CreatedAt = e.CreatedAt,
                    LastAccessedAt = e.LastAccessedAt,
                    ExpiresAt = e.ExpiresAt,
                    AccessCount = e.AccessCount,
                    Priority = e.Priority
                }).ToList();

                var stats = await _cacheManager.GetRegionStatisticsAsync(region, cancellationToken);

                return new CacheEntriesDto
                {
                    RegionId = regionId,
                    Entries = entryDtos,
                    TotalCount = (int)stats.EntryCount,
                    Skip = skip,
                    Take = take
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache entries for region {RegionId}", regionId);
                throw;
            }
        }

        /// <summary>
        /// Forces a refresh of specific cache entries or an entire region.
        /// </summary>
        public async Task RefreshCacheAsync(string regionId, string? key = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Enum.TryParse<CacheRegion>(regionId, true, out var region))
                {
                    throw new ArgumentException($"Invalid region ID: {regionId}");
                }

                if (!string.IsNullOrEmpty(key))
                {
                    // Refresh specific key
                    var refreshed = await _cacheManager.RefreshAsync(key, region, null, cancellationToken);
                    if (!refreshed)
                    {
                        throw new KeyNotFoundException($"Cache key '{key}' not found in region '{regionId}'");
                    }
                }
                else
                {
                    // Refresh entire region by clearing and allowing repopulation
                    await _cacheManager.ClearRegionAsync(region, cancellationToken);
                    _logger.LogInformation("Cleared region {Region} for refresh", region);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh cache for region {RegionId}", regionId);
                throw;
            }
        }
    }
}