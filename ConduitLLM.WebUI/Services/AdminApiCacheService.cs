using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing the Admin API client cache.
    /// </summary>
    public class AdminApiCacheService : IAdminApiCacheService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<AdminApiCacheService> _logger;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AdminApiCacheService"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public AdminApiCacheService(IAdminApiClient adminApiClient, ILogger<AdminApiCacheService> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public CacheStatistics GetCacheStatistics()
        {
            if (_adminApiClient is CachingAdminApiClient cachingClient)
            {
                return cachingClient.GetStatistics();
            }
            
            return new CacheStatistics
            {
                CacheHits = 0,
                CacheMisses = 0,
                HitRate = 0,
                ActiveCacheEntries = 0
            };
        }
        
        /// <inheritdoc />
        public void ClearAllCaches()
        {
            if (_adminApiClient is CachingAdminApiClient cachingClient)
            {
                cachingClient.ClearAllCaches();
                _logger.LogInformation("All Admin API caches cleared");
            }
            else
            {
                _logger.LogInformation("Admin API client does not support caching");
            }
        }
        
        /// <inheritdoc />
        public bool IsCachingEnabled()
        {
            return _adminApiClient is CachingAdminApiClient;
        }
    }
}