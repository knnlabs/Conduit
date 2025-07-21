using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Extends cache statistics collection with distributed system capabilities.
    /// </summary>
    public interface IDistributedCacheStatisticsCollector : ICacheStatisticsCollector
    {
        /// <summary>
        /// Gets the unique identifier for this collector instance.
        /// </summary>
        string InstanceId { get; }

        /// <summary>
        /// Gets aggregated statistics across all instances for a specific region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Aggregated statistics across all instances.</returns>
        Task<CacheStatistics> GetAggregatedStatisticsAsync(
            CacheRegion region, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets aggregated statistics across all instances for all regions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Aggregated statistics for all regions.</returns>
        Task<Dictionary<CacheRegion, CacheStatistics>> GetAllAggregatedStatisticsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics broken down by instance for a specific region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Statistics per instance.</returns>
        Task<Dictionary<string, CacheStatistics>> GetPerInstanceStatisticsAsync(
            CacheRegion region,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a list of all active collector instances.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of active instance identifiers.</returns>
        Task<IEnumerable<string>> GetActiveInstancesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronizes local statistics with the distributed store.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SynchronizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers this instance as active in the distributed system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RegisterInstanceAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregisters this instance from the distributed system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UnregisterInstanceAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when statistics are updated on any instance.
        /// </summary>
        event EventHandler<DistributedCacheStatisticsEventArgs>? DistributedStatisticsUpdated;
    }

    /// <summary>
    /// Event arguments for distributed cache statistics updates.
    /// </summary>
    public class DistributedCacheStatisticsEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the instance identifier that generated the update.
        /// </summary>
        public string InstanceId { get; }

        /// <summary>
        /// Gets the cache region.
        /// </summary>
        public CacheRegion Region { get; }

        /// <summary>
        /// Gets the updated statistics.
        /// </summary>
        public CacheStatistics Statistics { get; }

        /// <summary>
        /// Gets the timestamp of the update.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedCacheStatisticsEventArgs"/> class.
        /// </summary>
        public DistributedCacheStatisticsEventArgs(
            string instanceId, 
            CacheRegion region, 
            CacheStatistics statistics,
            DateTime timestamp)
        {
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            Region = region;
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            Timestamp = timestamp;
        }
    }
}