using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Service for checking IP filter rules
    /// </summary>
    public interface IIpFilterService
    {
        /// <summary>
        /// Checks if an IP address is allowed based on filter rules
        /// </summary>
        Task<bool> IsIpAllowedAsync(string ipAddress);
    }

    /// <summary>
    /// Implementation of IP filter service using shared IpAddressHelper utilities
    /// </summary>
    public class IpFilterService : IIpFilterService
    {
        private readonly IIpFilterRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IpFilterService> _logger;
        private const string CACHE_KEY = "ip_filters_enabled";
        private const int CACHE_DURATION_MINUTES = 5;

        /// <summary>
        /// Initializes a new instance of the IpFilterService
        /// </summary>
        public IpFilterService(
            IIpFilterRepository repository,
            IMemoryCache cache,
            ILogger<IpFilterService> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> IsIpAllowedAsync(string ipAddress)
        {
            try
            {
                // Get enabled filters from cache or database
                var filters = await _cache.GetOrCreateAsync(CACHE_KEY, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_DURATION_MINUTES);
                    return await _repository.GetEnabledAsync();
                });

                if (filters == null || !filters.Any())
                {
                    // No filters defined, allow all
                    return true;
                }

                var filtersList = filters.ToList();
                var hasWhitelist = filtersList.Any(f => f.FilterType == IpFilterConstants.WHITELIST);
                var hasBlacklist = filtersList.Any(f => f.FilterType == IpFilterConstants.BLACKLIST);

                // Check blacklist first - if IP is blacklisted, deny immediately
                if (hasBlacklist)
                {
                    foreach (var filter in filtersList.Where(f => f.FilterType == IpFilterConstants.BLACKLIST))
                    {
                        if (IpAddressHelper.IsIpInRange(ipAddress, filter.IpAddressOrCidr))
                        {
                            _logger.LogWarning("IP {IpAddress} is blacklisted by rule {Rule}",
                                ipAddress, filter.IpAddressOrCidr);
                            return false;
                        }
                    }
                }

                // If there's a whitelist, IP must be in it
                if (hasWhitelist)
                {
                    foreach (var filter in filtersList.Where(f => f.FilterType == IpFilterConstants.WHITELIST))
                    {
                        if (IpAddressHelper.IsIpInRange(ipAddress, filter.IpAddressOrCidr))
                        {
                            _logger.LogDebug("IP {IpAddress} is whitelisted by rule {Rule}",
                                ipAddress, filter.IpAddressOrCidr);
                            return true;
                        }
                    }

                    // Has whitelist but IP not in it
                    _logger.LogWarning("IP {IpAddress} is not in whitelist", ipAddress);
                    return false;
                }

                // No whitelist and not blacklisted
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking IP filter for {IpAddress}", ipAddress);
                // On error, default to allow to prevent blocking legitimate traffic
                return true;
            }
        }
    }
}