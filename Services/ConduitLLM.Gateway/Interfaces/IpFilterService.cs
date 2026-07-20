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

        /// <summary>
        /// Checks whether an IP address is allowed for a specific virtual key by that key's own per-key
        /// IP filters (applied in addition to the global rules). A key with no per-key rules is unrestricted.
        /// </summary>
        Task<bool> IsIpAllowedForVirtualKeyAsync(string ipAddress, int virtualKeyId);

        /// <summary>
        /// Invalidates the in-memory filter-rules cache so the next check reloads from the database.
        /// Called by the IpFilterChanged event handler on each replica when rules change, so updates
        /// take effect immediately instead of waiting for the cache TTL.
        /// </summary>
        void InvalidateCache();
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
        private const string PER_KEY_CACHE_KEY = "ip_filters_per_key";
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

        /// <inheritdoc/>
        public async Task<bool> IsIpAllowedForVirtualKeyAsync(string ipAddress, int virtualKeyId)
        {
            try
            {
                // Load all enabled per-key filters once, grouped by virtual key. One cache entry for
                // all keys keeps invalidation simple (cleared wholesale with the global cache on change).
                var perKey = await _cache.GetOrCreateAsync(PER_KEY_CACHE_KEY, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_DURATION_MINUTES);
                    var enabled = await _repository.GetEnabledPerKeyAsync();
                    return enabled
                        .GroupBy(f => f.VirtualKeyId!.Value)
                        .ToDictionary(
                            g => g.Key,
                            g => new PartitionedFilters(
                                g.Where(f => f.FilterType == IpFilterConstants.WHITELIST).ToList(),
                                g.Where(f => f.FilterType == IpFilterConstants.BLACKLIST).ToList()));
                });

                if (perKey == null || !perKey.TryGetValue(virtualKeyId, out var filters))
                {
                    // No per-key rules for this key — not restricted at the per-key level.
                    return true;
                }

                // defaultAllow = true: a key with only a per-key blacklist denies just those IPs, while a
                // per-key whitelist makes the key restrictive (only listed IPs may use it).
                var decision = IpFilterEvaluator.Evaluate(
                    ipAddress, filters.Whitelist, filters.Blacklist, defaultAllow: true);

                if (!decision.IsAllowed)
                {
                    _logger.LogWarning(
                        "IP {IpAddress} denied for virtual key {VirtualKeyId} by per-key IP filter: {Reason}",
                        ipAddress, virtualKeyId, decision.Reason);
                }

                return decision.IsAllowed;
            }
            catch (Exception ex)
            {
                // Fail-open for per-key filtering: an error loading a key's rules should not break the
                // key's access (global filtering still applies separately).
                _logger.LogError(ex,
                    "Error checking per-key IP filter for {IpAddress} / key {VirtualKeyId}; allowing (fail-open)",
                    ipAddress, virtualKeyId);
                return true;
            }
        }

        /// <inheritdoc/>
        public void InvalidateCache()
        {
            _cache.Remove(CACHE_KEY);
            _cache.Remove(PER_KEY_CACHE_KEY);
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
