using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Registry for managing and discovering cache regions and their configurations.
    /// </summary>
    public interface ICacheRegistry
    {
        /// <summary>
        /// Registers a cache region with its configuration.
        /// </summary>
        /// <param name="region">The cache region to register.</param>
        /// <param name="config">Configuration for the region.</param>
        void RegisterRegion(CacheRegion region, CacheRegionConfig config);

        /// <summary>
        /// Registers a custom cache region by name.
        /// </summary>
        /// <param name="regionName">Name of the custom region.</param>
        /// <param name="config">Configuration for the region.</param>
        void RegisterCustomRegion(string regionName, CacheRegionConfig config);

        /// <summary>
        /// Gets configuration for a specific region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <returns>Configuration for the region, or null if not registered.</returns>
        CacheRegionConfig? GetRegionConfig(CacheRegion region);

        /// <summary>
        /// Gets configuration for a custom region by name.
        /// </summary>
        /// <param name="regionName">Name of the custom region.</param>
        /// <returns>Configuration for the region, or null if not registered.</returns>
        CacheRegionConfig? GetCustomRegionConfig(string regionName);

        /// <summary>
        /// Gets all registered regions and their configurations.
        /// </summary>
        /// <returns>Dictionary of regions and their configurations.</returns>
        IReadOnlyDictionary<CacheRegion, CacheRegionConfig> GetAllRegions();

        /// <summary>
        /// Gets all registered custom regions and their configurations.
        /// </summary>
        /// <returns>Dictionary of custom regions and their configurations.</returns>
        IReadOnlyDictionary<string, CacheRegionConfig> GetAllCustomRegions();

        /// <summary>
        /// Checks if a region is registered.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <returns>True if registered, false otherwise.</returns>
        bool IsRegionRegistered(CacheRegion region);

        /// <summary>
        /// Checks if a custom region is registered.
        /// </summary>
        /// <param name="regionName">Name of the custom region.</param>
        /// <returns>True if registered, false otherwise.</returns>
        bool IsCustomRegionRegistered(string regionName);

        /// <summary>
        /// Updates configuration for an existing region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="config">New configuration.</param>
        /// <returns>True if updated, false if region not found.</returns>
        bool UpdateRegionConfig(CacheRegion region, CacheRegionConfig config);

        /// <summary>
        /// Unregisters a cache region.
        /// </summary>
        /// <param name="region">The cache region to unregister.</param>
        /// <returns>True if unregistered, false if not found.</returns>
        bool UnregisterRegion(CacheRegion region);

        /// <summary>
        /// Gets metadata about cache usage for a region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <returns>Usage metadata for the region.</returns>
        Task<CacheRegionMetadata?> GetRegionMetadataAsync(CacheRegion region);

        /// <summary>
        /// Discovers and registers cache regions from assemblies.
        /// </summary>
        /// <param name="assemblies">Assemblies to scan. If null, scans all loaded assemblies.</param>
        /// <returns>Number of regions discovered and registered.</returns>
        Task<int> DiscoverRegionsAsync(params System.Reflection.Assembly[]? assemblies);

        /// <summary>
        /// Event raised when a region is registered.
        /// </summary>
        event EventHandler<CacheRegionEventArgs>? RegionRegistered;

        /// <summary>
        /// Event raised when a region configuration is updated.
        /// </summary>
        event EventHandler<CacheRegionEventArgs>? RegionUpdated;

        /// <summary>
        /// Event raised when a region is unregistered.
        /// </summary>
        event EventHandler<CacheRegionEventArgs>? RegionUnregistered;
    }

    /// <summary>
    /// Metadata about cache region usage.
    /// </summary>
    public class CacheRegionMetadata
    {
        /// <summary>
        /// The cache region.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// Services that use this cache region.
        /// </summary>
        public List<string> ConsumerServices { get; set; } = new();

        /// <summary>
        /// Dependencies on other cache regions.
        /// </summary>
        public List<CacheRegion> Dependencies { get; set; } = new();

        /// <summary>
        /// Whether this region is currently active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Estimated memory usage in bytes.
        /// </summary>
        public long EstimatedMemoryUsage { get; set; }

        /// <summary>
        /// Number of entries in this region.
        /// </summary>
        public long EntryCount { get; set; }

        /// <summary>
        /// When the region was first registered.
        /// </summary>
        public DateTime RegisteredAt { get; set; }

        /// <summary>
        /// Last time the region was accessed.
        /// </summary>
        public DateTime? LastAccessedAt { get; set; }

        /// <summary>
        /// Custom metadata specific to the region.
        /// </summary>
        public Dictionary<string, object> CustomMetadata { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for cache region events.
    /// </summary>
    public class CacheRegionEventArgs : EventArgs
    {
        /// <summary>
        /// The cache region.
        /// </summary>
        public CacheRegion Region { get; set; }

        /// <summary>
        /// The region configuration.
        /// </summary>
        public CacheRegionConfig Config { get; set; } = null!;

        /// <summary>
        /// Whether this is a custom region.
        /// </summary>
        public bool IsCustomRegion { get; set; }

        /// <summary>
        /// Name of the custom region (if applicable).
        /// </summary>
        public string? CustomRegionName { get; set; }
    }
}