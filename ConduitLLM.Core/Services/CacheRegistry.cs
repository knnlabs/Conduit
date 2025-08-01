using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Attributes;
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
            if (config == null)
                throw new ArgumentNullException(nameof(config));

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
            if (config == null)
                throw new ArgumentNullException(nameof(config));

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
            if (config == null)
                throw new ArgumentNullException(nameof(config));

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

        public async Task<int> DiscoverRegionsAsync(params Assembly[]? assemblies)
        {
            var targetAssemblies = assemblies?.Length > 0 
                ? assemblies 
                : AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.FullName) && 
                            // Only scan ConduitLLM assemblies to avoid scanning all dependencies
                            a.FullName.StartsWith("ConduitLLM", StringComparison.OrdinalIgnoreCase) &&
                            // Skip test assemblies
                            !a.FullName.Contains(".Tests", StringComparison.OrdinalIgnoreCase) &&
                            !a.FullName.Contains(".Test.", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            var discoveredCount = 0;

            foreach (var assembly in targetAssemblies)
            {
                try
                {
                    discoveredCount += await DiscoverInAssemblyAsync(assembly);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to discover cache regions in assembly {Assembly}", 
                        assembly.FullName);
                }
            }

            _logger.LogInformation("Discovered and registered {Count} cache regions from {AssemblyCount} assemblies",
                discoveredCount, targetAssemblies.Length);

            return discoveredCount;
        }

        private async Task<int> DiscoverInAssemblyAsync(Assembly assembly)
        {
            var count = 0;
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                // Check for CacheRegion attributes on the type
                var regionAttrs = type.GetCustomAttributes<CacheRegionAttribute>();
                foreach (var attr in regionAttrs)
                {
                    RegisterDiscoveredRegion(attr, type);
                    count++;
                }

                // Check for CustomCacheRegion attributes
                var customAttrs = type.GetCustomAttributes<CustomCacheRegionAttribute>();
                foreach (var attr in customAttrs)
                {
                    RegisterDiscoveredCustomRegion(attr, type);
                    count++;
                }

                // Check for CacheConfigurationProvider
                var configProvider = type.GetCustomAttribute<CacheConfigurationProviderAttribute>();
                if (configProvider != null)
                {
                    count += await RegisterFromConfigurationProviderAsync(type, configProvider);
                }

                // Check methods and properties
                count += DiscoverInMembers(type);
            }

            return count;
        }

        private int DiscoverInMembers(Type type)
        {
            var count = 0;

            // Check methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var regionAttrs = method.GetCustomAttributes<CacheRegionAttribute>();
                foreach (var attr in regionAttrs)
                {
                    RegisterDiscoveredRegion(attr, type, method.Name);
                    count++;
                }

                // Track dependencies
                var dependencies = method.GetCustomAttributes<CacheDependencyAttribute>();
                foreach (var dep in dependencies)
                {
                    // Get the region from method's CacheRegionAttribute if available
                    var methodRegion = regionAttrs.FirstOrDefault()?.Region ?? CacheRegion.Default;
                    RegisterDependency(methodRegion, dep);
                }
            }

            // Check properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var property in properties)
            {
                var regionAttrs = property.GetCustomAttributes<CacheRegionAttribute>();
                foreach (var attr in regionAttrs)
                {
                    RegisterDiscoveredRegion(attr, type, property.Name);
                    count++;
                }
            }

            return count;
        }

        private void RegisterDiscoveredRegion(CacheRegionAttribute attr, Type consumerType, string? memberName = null)
        {
            // Get or create metadata
            if (!_metadata.TryGetValue(attr.Region, out var metadata))
            {
                metadata = new CacheRegionMetadata
                {
                    Region = attr.Region,
                    RegisteredAt = DateTime.UtcNow,
                    IsActive = true
                };
                _metadata[attr.Region] = metadata;
            }

            // Track consumer
            var consumerName = memberName != null 
                ? $"{consumerType.Name}.{memberName}" 
                : consumerType.Name;
            
            if (!metadata.ConsumerServices.Contains(consumerName))
            {
                metadata.ConsumerServices.Add(consumerName);
            }

            // Update configuration if not already registered
            if (!_regions.ContainsKey(attr.Region))
            {
                var config = new CacheRegionConfig
                {
                    Region = attr.Region,
                    Enabled = true,
                    DefaultTTL = attr.SuggestedTtlSeconds > 0
                        ? TimeSpan.FromSeconds(attr.SuggestedTtlSeconds)
                        : TimeSpan.FromMinutes(15),
                    UseDistributedCache = attr.RequiresDistributed,
                    UseMemoryCache = true,
                    Priority = attr.Priority,
                    EvictionPolicy = CacheEvictionPolicy.LRU
                };

                RegisterRegion(attr.Region, config);
            }

            _logger.LogDebug("Discovered cache region {Region} usage in {Consumer}",
                attr.Region, consumerName);
        }

        private void RegisterDiscoveredCustomRegion(CustomCacheRegionAttribute attr, Type consumerType)
        {
            var config = new CacheRegionConfig
            {
                Region = CacheRegion.Default, // Custom regions map to default
                Enabled = true,
                DefaultTTL = TimeSpan.FromSeconds(attr.DefaultTtlSeconds),
                MaxTTL = attr.MaxTtlSeconds > 0
                    ? TimeSpan.FromSeconds(attr.MaxTtlSeconds)
                    : null,
                UseDistributedCache = attr.UseDistributed,
                UseMemoryCache = attr.UseMemory,
                Priority = attr.Priority,
                EvictionPolicy = Enum.TryParse<CacheEvictionPolicy>(attr.EvictionPolicy, out var policy)
                    ? policy
                    : CacheEvictionPolicy.LRU
            };

            RegisterCustomRegion(attr.RegionName, config);

            _logger.LogDebug("Discovered custom cache region {RegionName} in {Consumer}",
                attr.RegionName, consumerType.Name);
        }

        private async Task<int> RegisterFromConfigurationProviderAsync(Type type, CacheConfigurationProviderAttribute attr)
        {
            var count = 0;

            try
            {
                // Try method first
                if (!string.IsNullOrWhiteSpace(attr.ConfigurationMethodName))
                {
                    var method = type.GetMethod(attr.ConfigurationMethodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (method != null)
                    {
                        var result = method.Invoke(null, null);
                        count += ProcessConfigurationResult(result);
                    }
                }

                // Try property
                if (!string.IsNullOrWhiteSpace(attr.ConfigurationPropertyName))
                {
                    var property = type.GetProperty(attr.ConfigurationPropertyName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (property != null)
                    {
                        var result = property.GetValue(null);
                        count += ProcessConfigurationResult(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get configuration from provider {Type}", type.Name);
            }

            return await Task.FromResult(count);
        }

        private int ProcessConfigurationResult(object? result)
        {
            if (result == null)
                return 0;

            var count = 0;

            if (result is CacheRegionConfig config)
            {
                RegisterRegion(config.Region, config);
                count = 1;
            }
            else if (result is IEnumerable<CacheRegionConfig> configs)
            {
                foreach (var cfg in configs)
                {
                    RegisterRegion(cfg.Region, cfg);
                    count++;
                }
            }

            return count;
        }

        private void RegisterDependency(CacheRegion region, CacheDependencyAttribute dependency)
        {
            if (_metadata.TryGetValue(region, out var metadata))
            {
                if (!metadata.Dependencies.Contains(dependency.DependsOn))
                {
                    metadata.Dependencies.Add(dependency.DependsOn);
                }

                // Store dependency type in custom metadata
                var depKey = $"dependency_{dependency.DependsOn}";
                metadata.CustomMetadata[depKey] = dependency.DependencyType.ToString();
            }
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