using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Configuration management methods for CacheManagementService
    /// </summary>
    public partial class CacheManagementService
    {
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

        /// <summary>
        /// Helper method to update region configuration
        /// </summary>
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

        /// <summary>
        /// Helper method to get policy type string representation
        /// </summary>
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

        /// <summary>
        /// Helper method to get display name for cache regions
        /// </summary>
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
                CacheRegion.Providers => "Provider Credentials Cache",
                CacheRegion.ModelCosts => "Model Cost Cache",
                CacheRegion.AudioStreams => "Audio Stream Cache",
                CacheRegion.Monitoring => "Monitoring Cache",
                _ => region.ToString()
            };
        }

        /// <summary>
        /// Helper method to calculate eviction rate
        /// </summary>
        private double CalculateEvictionRate(CacheRegionStatistics stats)
        {
            var totalOperations = stats.HitCount + stats.MissCount + stats.SetCount;
            return totalOperations > 0 ? (double)stats.EvictionCount / totalOperations * 100 : 0;
        }
    }
}