using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Policy engine that manages and applies cache policies.
    /// </summary>
    public class CachePolicyEngine : ICachePolicyEngine
    {
        private readonly ILogger<CachePolicyEngine> _logger;
        private readonly Dictionary<string, (ICachePolicy Policy, CacheRegion[]? Regions)> _policies;
        private readonly ReaderWriterLockSlim _lock;

        /// <summary>
        /// Gets or sets whether to throw exceptions on policy errors.
        /// </summary>
        public bool ThrowOnPolicyError { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the cache policy engine.
        /// </summary>
        public CachePolicyEngine(ILogger<CachePolicyEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _policies = new Dictionary<string, (ICachePolicy, CacheRegion[]?)>(StringComparer.OrdinalIgnoreCase);
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// Registers a policy with the engine.
        /// </summary>
        public void RegisterPolicy(ICachePolicy policy, CacheRegion[]? regions = null)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            if (!policy.Validate())
            {
                var message = $"Policy '{policy.Name}' failed validation";
                _logger.LogError(message);
                
                if (ThrowOnPolicyError)
                    throw new InvalidOperationException(message);
                
                return;
            }

            _lock.EnterWriteLock();
            try
            {
                _policies[policy.Name] = (policy, regions);
                _logger.LogInformation("Registered cache policy '{PolicyName}' of type {PolicyType} for regions: {Regions}",
                    policy.Name, policy.PolicyType, regions?.Select(r => r.ToString()) ?? new[] { "All" });
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Unregisters a policy from the engine.
        /// </summary>
        public bool UnregisterPolicy(string policyName)
        {
            if (string.IsNullOrWhiteSpace(policyName))
                return false;

            _lock.EnterWriteLock();
            try
            {
                if (_policies.Remove(policyName))
                {
                    _logger.LogInformation("Unregistered cache policy '{PolicyName}'", policyName);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets all registered policies.
        /// </summary>
        public IEnumerable<ICachePolicy> GetPolicies()
        {
            _lock.EnterReadLock();
            try
            {
                return _policies.Values.Select(p => p.Policy).ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets policies for a specific region.
        /// </summary>
        public IEnumerable<ICachePolicy> GetPoliciesForRegion(CacheRegion region)
        {
            _lock.EnterReadLock();
            try
            {
                return _policies.Values
                    .Where(p => p.Regions == null || p.Regions.Contains(region))
                    .Select(p => p.Policy)
                    .Where(p => p.IsEnabled)
                    .OrderByDescending(p => p.Priority)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets policies of a specific type.
        /// </summary>
        public IEnumerable<T> GetPolicies<T>(CacheRegion? region = null) where T : ICachePolicy
        {
            var policies = region.HasValue ? GetPoliciesForRegion(region.Value) : GetPolicies();
            return policies.OfType<T>().Where(p => p.IsEnabled);
        }

        /// <summary>
        /// Applies TTL policies to determine expiration time.
        /// </summary>
        public DateTime? ApplyTtlPolicies(ICacheEntry entry, CachePolicyContext context)
        {
            var ttlPolicies = GetPolicies<ITtlPolicy>(context.Region)
                .OrderByDescending(p => p.Priority)
                .ToList();

            if (!ttlPolicies.Any())
                return null;

            DateTime? shortestExpiration = null;

            foreach (var policy in ttlPolicies)
            {
                try
                {
                    var expiration = policy.CalculateExpiration(entry, context);
                    
                    if (expiration.HasValue)
                    {
                        if (!shortestExpiration.HasValue || expiration.Value < shortestExpiration.Value)
                        {
                            shortestExpiration = expiration.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying TTL policy '{PolicyName}'", policy.Name);
                    
                    if (ThrowOnPolicyError)
                        throw;
                }
            }

            return shortestExpiration;
        }

        /// <summary>
        /// Applies size policies to check if an entry can be added.
        /// </summary>
        public bool ApplySizePolicies(ICacheEntry entry, long currentSize, CachePolicyContext context)
        {
            var sizePolicies = GetPolicies<ISizePolicy>(context.Region)
                .OrderByDescending(p => p.Priority)
                .ToList();

            if (!sizePolicies.Any())
                return true; // No size restrictions

            foreach (var policy in sizePolicies)
            {
                try
                {
                    if (policy.WouldExceedLimit(entry, currentSize))
                    {
                        _logger.LogDebug("Size policy '{PolicyName}' would be exceeded by adding entry '{Key}'",
                            policy.Name, entry.Key);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying size policy '{PolicyName}'", policy.Name);
                    
                    if (ThrowOnPolicyError)
                        throw;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies eviction policies to select entries for removal.
        /// </summary>
        public async Task<IEnumerable<ICacheEntry>> ApplyEvictionPoliciesAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            var evictionPolicies = GetPolicies<IEvictionPolicy>(context.Region)
                .OrderByDescending(p => p.Priority)
                .ToList();

            if (!evictionPolicies.Any())
            {
                // Default: evict oldest entries
                return entries
                    .OrderBy(e => e.CreatedAt)
                    .TakeWhile((e, i) => i == 0 || entries.Take(i).Sum(x => x.SizeInBytes ?? 1) < spaceNeeded)
                    .ToList();
            }

            // Use the highest priority eviction policy
            var primaryPolicy = evictionPolicies.First();

            try
            {
                return await primaryPolicy.SelectForEvictionAsync(entries, spaceNeeded, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying eviction policy '{PolicyName}'", primaryPolicy.Name);
                
                if (ThrowOnPolicyError)
                    throw;

                // Fallback to simple LRU
                return entries
                    .OrderBy(e => e.LastAccessedAt)
                    .TakeWhile((e, i) => i == 0 || entries.Take(i).Sum(x => x.SizeInBytes ?? 1) < spaceNeeded)
                    .ToList();
            }
        }

        /// <summary>
        /// Notifies policies when an entry is accessed.
        /// </summary>
        public void OnEntryAccessed(ICacheEntry entry, CachePolicyContext context)
        {
            var evictionPolicies = GetPolicies<IEvictionPolicy>(context.Region);

            foreach (var policy in evictionPolicies)
            {
                try
                {
                    policy.OnEntryAccessed(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in policy '{PolicyName}' OnEntryAccessed", policy.Name);
                    
                    if (ThrowOnPolicyError)
                        throw;
                }
            }
        }

        /// <summary>
        /// Validates all registered policies.
        /// </summary>
        public Dictionary<string, bool> ValidatePolicies()
        {
            var results = new Dictionary<string, bool>();

            _lock.EnterReadLock();
            try
            {
                foreach (var (name, (policy, _)) in _policies)
                {
                    try
                    {
                        results[name] = policy.Validate();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error validating policy '{PolicyName}'", name);
                        results[name] = false;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return results;
        }

        /// <summary>
        /// Disposes of the policy engine resources.
        /// </summary>
        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}