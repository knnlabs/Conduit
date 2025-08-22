using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.DTOs.Cache;
using MassTransit;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing cache configuration and operations through the Admin API.
    /// </summary>
    /// <inheritdoc />
    public partial class CacheManagementService : ICacheManagementService
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
        /// Helper method to format byte sizes for display
        /// </summary>
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