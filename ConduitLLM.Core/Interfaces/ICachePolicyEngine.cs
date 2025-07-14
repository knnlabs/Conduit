using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for the cache policy engine that manages and applies cache policies.
    /// </summary>
    public interface ICachePolicyEngine
    {
        /// <summary>
        /// Registers a policy with the engine.
        /// </summary>
        /// <param name="policy">The policy to register.</param>
        /// <param name="regions">Optional regions where this policy applies. If null, applies to all regions.</param>
        void RegisterPolicy(ICachePolicy policy, CacheRegion[]? regions = null);

        /// <summary>
        /// Unregisters a policy from the engine.
        /// </summary>
        /// <param name="policyName">The name of the policy to unregister.</param>
        /// <returns>True if the policy was found and removed.</returns>
        bool UnregisterPolicy(string policyName);

        /// <summary>
        /// Gets all registered policies.
        /// </summary>
        /// <returns>Collection of registered policies.</returns>
        IEnumerable<ICachePolicy> GetPolicies();

        /// <summary>
        /// Gets policies for a specific region.
        /// </summary>
        /// <param name="region">The cache region.</param>
        /// <returns>Policies applicable to the region.</returns>
        IEnumerable<ICachePolicy> GetPoliciesForRegion(CacheRegion region);

        /// <summary>
        /// Gets policies of a specific type.
        /// </summary>
        /// <typeparam name="T">The policy interface type.</typeparam>
        /// <param name="region">Optional region filter.</param>
        /// <returns>Policies of the specified type.</returns>
        IEnumerable<T> GetPolicies<T>(CacheRegion? region = null) where T : ICachePolicy;

        /// <summary>
        /// Applies TTL policies to determine expiration time.
        /// </summary>
        /// <param name="entry">The cache entry.</param>
        /// <param name="context">The policy context.</param>
        /// <returns>The calculated expiration time.</returns>
        DateTime? ApplyTtlPolicies(ICacheEntry entry, CachePolicyContext context);

        /// <summary>
        /// Applies size policies to check if an entry can be added.
        /// </summary>
        /// <param name="entry">The cache entry.</param>
        /// <param name="currentSize">Current cache size.</param>
        /// <param name="context">The policy context.</param>
        /// <returns>True if the entry can be added.</returns>
        bool ApplySizePolicies(ICacheEntry entry, long currentSize, CachePolicyContext context);

        /// <summary>
        /// Applies eviction policies to select entries for removal.
        /// </summary>
        /// <param name="entries">All cache entries.</param>
        /// <param name="spaceNeeded">Amount of space needed.</param>
        /// <param name="context">The policy context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Entries to evict.</returns>
        Task<IEnumerable<ICacheEntry>> ApplyEvictionPoliciesAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifies policies when an entry is accessed.
        /// </summary>
        /// <param name="entry">The accessed entry.</param>
        /// <param name="context">The policy context.</param>
        void OnEntryAccessed(ICacheEntry entry, CachePolicyContext context);

        /// <summary>
        /// Validates all registered policies.
        /// </summary>
        /// <returns>Validation results.</returns>
        Dictionary<string, bool> ValidatePolicies();

        /// <summary>
        /// Gets or sets whether to throw exceptions on policy errors.
        /// </summary>
        bool ThrowOnPolicyError { get; set; }
    }
}