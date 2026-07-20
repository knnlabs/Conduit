using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Gateway.Interfaces
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
    /// Implementation of IP filter service. Uses the shared <see cref="IpFilterEvaluator"/> so the
    /// Gateway data plane and Admin control plane apply identical precedence, and honors the
    /// DB-persisted <c>IpFilter:DefaultAllow</c> policy (read live from <see cref="IGlobalSettingsCacheService"/>).
    /// </summary>
    public class IpFilterService : IIpFilterService
    {
        private readonly IIpFilterRepository _repository;
        private readonly IGlobalSettingsCacheService _globalSettings;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IpFilterService> _logger;
        private const string CACHE_KEY = "ip_filters_enabled";
        private const string DEFAULT_ALLOW_SETTING_KEY = "IpFilter:DefaultAllow";
        private const int CACHE_DURATION_MINUTES = 5;

        /// <summary>
        /// Initializes a new instance of the IpFilterService
        /// </summary>
        public IpFilterService(
            IIpFilterRepository repository,
            IGlobalSettingsCacheService globalSettings,
            IMemoryCache cache,
            ILogger<IpFilterService> logger)
        {
            _repository = repository;
            _globalSettings = globalSettings;
            _cache = cache;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> IsIpAllowedAsync(string ipAddress)
        {
            // Resolve the default-allow policy up front from the in-memory settings cache so it is
            // still available if the filter-rule load below fails (see the nuanced fail posture).
            var defaultAllow = await GetDefaultAllowAsync();

            try
            {
                // Get enabled filters from cache or database, partitioned once at population time
                // so the per-request path is a single pass.
                var filters = await _cache.GetOrCreateAsync(CACHE_KEY, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_DURATION_MINUTES);
                    var enabled = await _repository.GetEnabledAsync();
                    return new PartitionedFilters(
                        enabled.Where(f => f.FilterType == IpFilterConstants.WHITELIST).ToList(),
                        enabled.Where(f => f.FilterType == IpFilterConstants.BLACKLIST).ToList());
                });

                if (filters == null || (filters.Whitelist.Count == 0 && filters.Blacklist.Count == 0))
                {
                    // No rules defined — fall through to the default-allow policy.
                    return defaultAllow;
                }

                var decision = IpFilterEvaluator.Evaluate(
                    ipAddress, filters.Whitelist, filters.Blacklist, defaultAllow);

                if (!decision.IsAllowed)
                {
                    _logger.LogWarning("IP {IpAddress} denied by database IP filter: {Reason}",
                        ipAddress, decision.Reason);
                }

                return decision.IsAllowed;
            }
            catch (Exception ex)
            {
                // Nuanced fail posture: apply the default-allow policy. This fails OPEN when the
                // deployment is permissive (DefaultAllow=true) and fails CLOSED when it is restrictive
                // (DefaultAllow=false) — so a DB blip cannot silently disable an allowlist.
                _logger.LogError(ex,
                    "Error checking IP filter for {IpAddress}; applying default-allow policy ({DefaultAllow}) as fail posture",
                    ipAddress, defaultAllow);
                return defaultAllow;
            }
        }

        private async Task<bool> GetDefaultAllowAsync()
        {
            try
            {
                var value = await _globalSettings.GetSettingValueAsync(DEFAULT_ALLOW_SETTING_KEY);
                return value == null || !bool.TryParse(value, out var parsed) ? true : parsed;
            }
            catch
            {
                // Settings cache unavailable — default to allow (availability) as a last resort.
                return true;
            }
        }

        private sealed record PartitionedFilters(
            List<IpFilterEntity> Whitelist,
            List<IpFilterEntity> Blacklist);
    }
}
