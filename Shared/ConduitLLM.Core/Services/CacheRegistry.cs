using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Registry implementation for managing and discovering cache regions.
    /// </summary>
    public class CacheRegistry : ICacheRegistry
    {
        private readonly ConcurrentDictionary<CacheRegion, CacheRegionConfig> _regions;
        private readonly ConcurrentDictionary<string, CacheRegionConfig> _customRegions;
        private readonly ConcurrentDictionary<CacheRegion, CacheRegionMetadata> _metadata;
        private readonly ILogger<CacheRegistry> _logger;
        private readonly ICacheManager? _cacheManager;

        public event EventHandler<CacheRegionEventArgs>? RegionRegistered;
        public event EventHandler<CacheRegionEventArgs>? RegionUpdated;
        public event EventHandler<CacheRegionEventArgs>? RegionUnregistered;

        public CacheRegistry(ILogger<CacheRegistry> logger, ICacheManager? cacheManager = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheManager = cacheManager;
            _regions = new ConcurrentDictionary<CacheRegion, CacheRegionConfig>();
            _customRegions = new ConcurrentDictionary<string, CacheRegionConfig>();
            _metadata = new ConcurrentDictionary<CacheRegion, CacheRegionMetadata>();

            // Initialize with default regions
            InitializeDefaultRegions();
        }

        public void RegisterRegion(CacheRegion region, CacheRegionConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            config.Region = region; // Ensure region is set correctly
            _regions[region] = config;

            // Initialize metadata if not exists
            _metadata.TryAdd(region, new CacheRegionMetadata
            {
                Region = region,
                RegisteredAt = DateTime.UtcNow,
                IsActive = config.Enabled
            });

            // Update cache manager if available
            _cacheManager?.UpdateRegionConfigAsync(config);

            _logger.LogInformation("Registered cache region {Region} with TTL {TTL}",
                region, config.DefaultTTL);

            RegionRegistered?.Invoke(this, new CacheRegionEventArgs
            {
                Region = region,
                Config = config,
                IsCustomRegion = false
            });
        }

        public void RegisterCustomRegion(string regionName, CacheRegionConfig config)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                throw new ArgumentException("Region name cannot be empty", nameof(regionName));
            ArgumentNullException.ThrowIfNull(config);

            _customRegions[regionName] = config;

            _logger.LogInformation("Registered custom cache region {RegionName} with TTL {TTL}",
                regionName, config.DefaultTTL);

            RegionRegistered?.Invoke(this, new CacheRegionEventArgs
            {
                Region = CacheRegion.Default,
                Config = config,
                IsCustomRegion = true,
                CustomRegionName = regionName
            });
        }

        public void RegisterDescriptor(CacheRegionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            RegisterRegion(descriptor.Region, descriptor.Configuration);

            var metadata = _metadata[descriptor.Region];
            foreach (var consumer in descriptor.Consumers ?? [])
            {
                if (!metadata.ConsumerServices.Contains(consumer, StringComparer.Ordinal))
                    metadata.ConsumerServices.Add(consumer);
            }

            foreach (var dependency in descriptor.Dependencies ?? [])
            {
                if (!metadata.Dependencies.Contains(dependency))
                    metadata.Dependencies.Add(dependency);
            }
        }

        public CacheRegionConfig? GetRegionConfig(CacheRegion region)
        {
            return _regions.TryGetValue(region, out var config) ? config : null;
        }

        public CacheRegionConfig? GetCustomRegionConfig(string regionName)
        {
            return _customRegions.TryGetValue(regionName, out var config) ? config : null;
        }

        public IReadOnlyDictionary<CacheRegion, CacheRegionConfig> GetAllRegions()
        {
            return _regions;
        }

        public IReadOnlyDictionary<string, CacheRegionConfig> GetAllCustomRegions()
        {
            return _customRegions;
        }

        public bool IsRegionRegistered(CacheRegion region)
        {
            return _regions.ContainsKey(region);
        }

        public bool IsCustomRegionRegistered(string regionName)
        {
            return _customRegions.ContainsKey(regionName);
        }

        public bool UpdateRegionConfig(CacheRegion region, CacheRegionConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (!_regions.ContainsKey(region))
                return false;

            config.Region = region;
            _regions[region] = config;

            // Update cache manager if available
            _cacheManager?.UpdateRegionConfigAsync(config);

            _logger.LogInformation("Updated cache region {Region} configuration", region);

            RegionUpdated?.Invoke(this, new CacheRegionEventArgs
            {
                Region = region,
                Config = config,
                IsCustomRegion = false
            });

            return true;
        }

        public bool UnregisterRegion(CacheRegion region)
        {
            if (_regions.TryRemove(region, out var config))
            {
                _metadata.TryRemove(region, out _);

                _logger.LogInformation("Unregistered cache region {Region}", region);

                RegionUnregistered?.Invoke(this, new CacheRegionEventArgs
                {
                    Region = region,
                    Config = config,
                    IsCustomRegion = false
                });

                return true;
            }

            return false;
        }

        public async Task<CacheRegionMetadata?> GetRegionMetadataAsync(CacheRegion region)
        {
            if (!_metadata.TryGetValue(region, out var metadata))
                return null;

            // Update with current statistics if cache manager available
            if (_cacheManager != null)
            {
                try
                {
                    var stats = await _cacheManager.GetRegionStatisticsAsync(region);
                    metadata.EntryCount = stats.EntryCount;
                    metadata.EstimatedMemoryUsage = stats.TotalSizeBytes;
                    metadata.IsActive = stats.HitCount + stats.MissCount > 0;
                    
                    if (stats.HitCount + stats.MissCount > 0)
                    {
                        metadata.LastAccessedAt = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update metadata for region {Region}", region);
                }
            }

            return metadata;
        }

        private void InitializeDefaultRegions()
        {
            // Register all enum values with sensible defaults
            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                if (region == CacheRegion.Default)
                    continue;

                var config = GetDefaultConfigForRegion(region);
                RegisterRegion(region, config);
            }
        }

        private CacheRegionConfig GetDefaultConfigForRegion(CacheRegion region)
        {
            var (ttl, priority, distributed) = region switch
            {
                CacheRegion.VirtualKeys => (TimeSpan.FromMinutes(30), 100, true),
                CacheRegion.RateLimits => (TimeSpan.FromMinutes(5), 90, true),
                CacheRegion.ProviderHealth => (TimeSpan.FromMinutes(1), 80, true),
                CacheRegion.ModelMetadata => (TimeSpan.FromHours(24), 50, true),
                CacheRegion.AuthTokens => (TimeSpan.FromHours(1), 95, true),
                CacheRegion.IpFilters => (TimeSpan.FromMinutes(15), 85, true),
                CacheRegion.AsyncTasks => (TimeSpan.FromHours(2), 40, true),
                CacheRegion.ProviderResponses => (TimeSpan.FromMinutes(60), 60, true),
                CacheRegion.Embeddings => (TimeSpan.FromDays(7), 70, true),
                CacheRegion.GlobalSettings => (TimeSpan.FromMinutes(30), 75, true),
                CacheRegion.Providers => (TimeSpan.FromHours(4), 80, true),
                CacheRegion.ModelCosts => (TimeSpan.FromHours(12), 55, true),
                CacheRegion.AudioStreams => (TimeSpan.FromMinutes(10), 30, false),
                CacheRegion.Monitoring => (TimeSpan.FromMinutes(5), 45, false),
                _ => (TimeSpan.FromMinutes(15), 50, false)
            };

            return new CacheRegionConfig
            {
                Region = region,
                Enabled = true,
                DefaultTTL = ttl,
                Priority = priority,
                UseDistributedCache = distributed,
                UseMemoryCache = true,
                EvictionPolicy = CacheEvictionPolicy.LRU,
                EnableDetailedStats = true
            };
        }
    }
}
