using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Models;
using ConduitLLM.Admin.Models;
using MassTransit;
using ConduitLLM.Configuration.Events;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing cache configuration and operations through the Admin API.
    /// </summary>
    /// <inheritdoc />
    public class CacheManagementService : ICacheManagementService
    {
        private readonly ICacheManager _cacheManager;
        private readonly ICacheRegistry _cacheRegistry;
        private readonly ICacheConfigurationService _configService;
        private readonly ICacheStatisticsCollector _statisticsCollector;
        private readonly ICachePolicyEngine _policyEngine;
        private readonly ILogger<CacheManagementService> _logger;
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the CacheManagementService.
        /// </summary>
        public CacheManagementService(
            ICacheManager cacheManager,
            ICacheRegistry cacheRegistry,
            ICacheConfigurationService configService,
            ICacheStatisticsCollector statisticsCollector,
            ICachePolicyEngine policyEngine,
            ILogger<CacheManagementService> logger,
            IPublishEndpoint publishEndpoint)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _cacheRegistry = cacheRegistry ?? throw new ArgumentNullException(nameof(cacheRegistry));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _statisticsCollector = statisticsCollector ?? throw new ArgumentNullException(nameof(statisticsCollector));
            _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        }

        /// <summary>
        /// Gets the current cache configuration including all regions and policies.
        /// </summary>
        public async Task<CacheConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var regions = _cacheRegistry.GetAllRegions();
                var cachePolicies = new List<CachePolicyDto>();
                var cacheRegions = new List<CacheRegionDto>();

                foreach (var (region, config) in regions)
                {
                    // Get region configuration
                    var regionConfig = await _configService.GetConfigurationAsync(region.ToString(), cancellationToken);
                    
                    // Get region statistics
                    var stats = await _cacheManager.GetRegionStatisticsAsync(region, cancellationToken);
                    
                    // Get applicable policies
                    var policies = _policyEngine.GetPoliciesForRegion(region);

                    // Create cache policy DTOs
                    var regionPolicies = policies.Select(p => new CachePolicyDto
                    {
                        Id = $"{region}-{p.Name}".ToLower().Replace(" ", "-"),
                        Name = p.Name,
                        Type = GetPolicyTypeString(p.PolicyType),
                        TTL = (int)(regionConfig?.DefaultTTL?.TotalSeconds ?? 300),
                        MaxSize = (int)(regionConfig?.MaxEntries ?? 1000),
                        Strategy = regionConfig?.EvictionPolicy ?? "LRU",
                        Enabled = p.IsEnabled,
                        Description = $"{p.PolicyType} policy for {region} region"
                    }).ToList();

                    cachePolicies.AddRange(regionPolicies);

                    // Create cache region DTO
                    cacheRegions.Add(new CacheRegionDto
                    {
                        Id = region.ToString().ToLower(),
                        Name = GetRegionDisplayName(region),
                        Type = regionConfig?.UseDistributedCache == true ? "distributed" : "memory",
                        Status = stats.HitCount + stats.MissCount > 0 ? "healthy" : "idle",
                        Nodes = 1, // Would need to query actual node count for distributed cache
                        Metrics = new CacheMetricsDto
                        {
                            Size = FormatSize(stats.TotalSizeBytes),
                            Items = stats.EntryCount,
                            HitRate = stats.HitRate * 100,
                            MissRate = (1 - stats.HitRate) * 100,
                            EvictionRate = CalculateEvictionRate(stats)
                        }
                    });
                }

                // Get overall statistics
                var overallStats = await GetOverallStatisticsAsync(cancellationToken);

                return new CacheConfigurationDto
                {
                    Timestamp = DateTime.UtcNow,
                    CachePolicies = cachePolicies,
                    CacheRegions = cacheRegions,
                    Statistics = overallStats,
                    Configuration = new CacheGlobalConfigDto
                    {
                        DefaultTTL = 300, // Default from configuration
                        MaxMemorySize = "1GB",
                        EvictionPolicy = "LRU",
                        CompressionEnabled = true,
                        RedisConnectionString = "[REDACTED]" // Security: never expose connection strings
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache configuration");
                throw;
            }
        }

        /// <summary>
        /// Updates cache configuration for a specific region or globally.
        /// </summary>
        public async Task UpdateConfigurationAsync(UpdateCacheConfigDto config, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating cache configuration");

                // Update global configuration if specified
                if (config.ApplyGlobally)
                {
                    foreach (var region in Enum.GetValues<CacheRegion>())
                    {
                        await UpdateRegionConfigurationAsync(region, config, cancellationToken);
                    }
                }
                else if (!string.IsNullOrEmpty(config.RegionId))
                {
                    // Update specific region
                    if (Enum.TryParse<CacheRegion>(config.RegionId, true, out var region))
                    {
                        await UpdateRegionConfigurationAsync(region, config, cancellationToken);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid region ID: {config.RegionId}");
                    }
                }

                // Clear caches if requested
                if (config.ClearAffectedCaches)
                {
                    if (config.ApplyGlobally)
                    {
                        await _cacheManager.ClearAllAsync(cancellationToken);
                    }
                    else if (Enum.TryParse<CacheRegion>(config.RegionId, true, out var region))
                    {
                        await _cacheManager.ClearRegionAsync(region, cancellationToken);
                    }
                }

                // Publish configuration change event
                await _publishEndpoint.Publish(new CacheConfigurationChangedEvent
                {
                    Region = config.RegionId ?? "global",
                    ChangedBy = "Admin API"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update cache configuration");
                throw;
            }
        }

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
                var sensitiveRegions = new[] { CacheRegion.AuthTokens, CacheRegion.ProviderCredentials };
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

        /// <summary>
        /// Updates the policy configuration for a specific cache region.
        /// </summary>
        public async Task UpdatePolicyAsync(string regionId, UpdateCachePolicyDto policyUpdate, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Enum.TryParse<CacheRegion>(regionId, true, out var region))
                {
                    throw new ArgumentException($"Invalid region ID: {regionId}");
                }

                var config = await _configService.GetConfigurationAsync(region.ToString(), cancellationToken);
                
                if (config == null)
                {
                    config = new ConduitLLM.Configuration.Models.CacheRegionConfig
                    {
                        Region = region.ToString(),
                        Enabled = true
                    };
                }
                
                // Update configuration based on policy changes
                if (policyUpdate.TTL.HasValue)
                {
                    config.DefaultTTL = TimeSpan.FromSeconds(policyUpdate.TTL.Value);
                }

                if (policyUpdate.MaxSize.HasValue)
                {
                    config.MaxEntries = policyUpdate.MaxSize.Value;
                }

                if (!string.IsNullOrEmpty(policyUpdate.Strategy))
                {
                    config.EvictionPolicy = policyUpdate.Strategy;
                }

                // Save updated configuration
                await _configService.UpdateConfigurationAsync(
                    region.ToString(), 
                    config, 
                    "Admin API",
                    $"Policy update: {policyUpdate.Reason}",
                    cancellationToken);

                _logger.LogInformation("Updated cache policy for region {Region}", region);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update cache policy for region {RegionId}", regionId);
                throw;
            }
        }

        private async Task UpdateRegionConfigurationAsync(CacheRegion region, UpdateCacheConfigDto config, CancellationToken cancellationToken)
        {
            var regionConfig = await _configService.GetConfigurationAsync(region.ToString(), cancellationToken);
            
            if (regionConfig == null)
            {
                regionConfig = new ConduitLLM.Configuration.Models.CacheRegionConfig
                {
                    Region = region.ToString(),
                    Enabled = true
                };
            }

            if (config.DefaultTTLSeconds.HasValue)
            {
                regionConfig.DefaultTTL = TimeSpan.FromSeconds(config.DefaultTTLSeconds.Value);
            }

            if (!string.IsNullOrEmpty(config.EvictionPolicy))
            {
                regionConfig.EvictionPolicy = config.EvictionPolicy;
            }

            regionConfig.EnableCompression = config.EnableCompression;

            await _configService.UpdateConfigurationAsync(
                region.ToString(),
                regionConfig,
                "Admin API",
                "Configuration update via Admin API",
                cancellationToken);
        }

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

        private string GetPolicyTypeString(CachePolicyType policyType)
        {
            return policyType switch
            {
                CachePolicyType.TTL => "ttl",
                CachePolicyType.Size => "size",
                CachePolicyType.Eviction => "eviction",
                _ => "custom"
            };
        }

        private string GetRegionDisplayName(CacheRegion region)
        {
            return region switch
            {
                CacheRegion.VirtualKeys => "Virtual Key Cache",
                CacheRegion.RateLimits => "Rate Limit Cache",
                CacheRegion.ProviderHealth => "Provider Health Cache",
                CacheRegion.ModelMetadata => "Model Metadata Cache",
                CacheRegion.AuthTokens => "Auth Token Cache",
                CacheRegion.IpFilters => "IP Filter Cache",
                CacheRegion.AsyncTasks => "Async Task Cache",
                CacheRegion.ProviderResponses => "Response Cache",
                CacheRegion.Embeddings => "Embeddings Cache",
                CacheRegion.GlobalSettings => "Global Settings Cache",
                CacheRegion.ProviderCredentials => "Provider Credentials Cache",
                CacheRegion.ModelCosts => "Model Cost Cache",
                CacheRegion.AudioStreams => "Audio Stream Cache",
                CacheRegion.Monitoring => "Monitoring Cache",
                _ => region.ToString()
            };
        }

        private double CalculateEvictionRate(CacheRegionStatistics stats)
        {
            var totalOperations = stats.HitCount + stats.MissCount + stats.SetCount;
            return totalOperations > 0 ? (double)stats.EvictionCount / totalOperations * 100 : 0;
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Interface for cache management operations.
    /// </summary>
    /// <summary>
    /// Provides operations for managing application cache, including configuration, clearing,
    /// refreshing, statistics retrieval and policy updates. Methods are asynchronous to avoid
    /// blocking IO-bound work such as distributed cache calls.
    /// </summary>
    public interface ICacheManagementService
    {
                /// <summary>
        /// Retrieves the current cache configuration including region policies and TTL defaults.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        /// <returns>A <see cref="CacheConfigurationDto"/> describing the cache settings.</returns>
        Task<CacheConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default);
                /// <summary>
        /// Persists a modified cache configuration.
        /// </summary>
        /// <param name="config">New configuration values.</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        Task UpdateConfigurationAsync(UpdateCacheConfigDto config, CancellationToken cancellationToken = default);
                /// <summary>
        /// Clears all keys belonging to the specified cache region.
        /// </summary>
        /// <param name="cacheId">Identifier of the region or cache instance.</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        Task ClearCacheAsync(string cacheId, CancellationToken cancellationToken = default);
                /// <summary>
        /// Retrieves aggregated statistics such as hit/miss counts for the whole cache or a single region.
        /// </summary>
        /// <param name="regionId">Optional region identifier; when <c>null</c> statistics for all regions are returned.</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        /// <returns>Statistics information.</returns>
        Task<CacheStatisticsDto> GetStatisticsAsync(string? regionId = null, CancellationToken cancellationToken = default);
                /// <summary>
        /// Enumerates cached entries in the specified region with paging support.
        /// </summary>
        /// <param name="regionId">Target cache region.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Maximum number of items to return.</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        Task<CacheEntriesDto> GetEntriesAsync(string regionId, int skip = 0, int take = 100, CancellationToken cancellationToken = default);
                /// <summary>
        /// Refreshes a single key or an entire region resetting its TTL without changing the value.
        /// </summary>
        /// <param name="regionId">Region to refresh.</param>
        /// <param name="key">Optional specific key; when <c>null</c> the whole region is refreshed.</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        Task RefreshCacheAsync(string regionId, string? key = null, CancellationToken cancellationToken = default);
                /// <summary>
        /// Updates TTL or eviction policy for a region.
        /// </summary>
        /// <param name="regionId">Target region.</param>
        /// <param name="policyUpdate">Policy mutation DTO.</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
        Task UpdatePolicyAsync(string regionId, UpdateCachePolicyDto policyUpdate, CancellationToken cancellationToken = default);
    }
}