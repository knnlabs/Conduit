using System;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Policies
{
    /// <summary>
    /// Base class for cache policies.
    /// </summary>
    public abstract class CachePolicyBase : ICachePolicy
    {
        /// <summary>
        /// Gets or sets the policy name.
        /// </summary>
        public virtual string Name { get; protected set; }

        /// <summary>
        /// Gets the policy type.
        /// </summary>
        public abstract CachePolicyType PolicyType { get; }

        /// <summary>
        /// Gets or sets whether this policy is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the priority of this policy.
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Initializes a new instance of the policy.
        /// </summary>
        protected CachePolicyBase(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public virtual bool Validate()
        {
            return !string.IsNullOrWhiteSpace(Name) && Priority >= 0 && Priority <= 100;
        }
    }

    /// <summary>
    /// Base class for TTL policies.
    /// </summary>
    public abstract class TtlPolicyBase : CachePolicyBase, ITtlPolicy
    {
        /// <summary>
        /// Gets the policy type.
        /// </summary>
        public override CachePolicyType PolicyType => CachePolicyType.TTL;

        /// <summary>
        /// Initializes a new instance of the TTL policy.
        /// </summary>
        protected TtlPolicyBase(string name) : base(name)
        {
        }

        /// <summary>
        /// Calculates the expiration time for a cache entry.
        /// </summary>
        public abstract DateTime? CalculateExpiration(ICacheEntry entry, CachePolicyContext context);

        /// <summary>
        /// Determines if an entry should be refreshed.
        /// </summary>
        public virtual bool ShouldRefresh(ICacheEntry entry, CachePolicyContext context)
        {
            if (entry.ExpiresAt == null)
                return false;

            var timeToExpire = entry.ExpiresAt.Value - DateTime.UtcNow;
            var totalLifetime = entry.ExpiresAt.Value - entry.CreatedAt;

            // Refresh if less than 20% of lifetime remains
            return timeToExpire.TotalMilliseconds < totalLifetime.TotalMilliseconds * 0.2;
        }
    }

    /// <summary>
    /// Base class for size policies.
    /// </summary>
    public abstract class SizePolicyBase : CachePolicyBase, ISizePolicy
    {
        /// <summary>
        /// Gets the policy type.
        /// </summary>
        public override CachePolicyType PolicyType => CachePolicyType.Size;

        /// <summary>
        /// Gets or sets the maximum allowed size.
        /// </summary>
        public long? MaxSize { get; set; }

        /// <summary>
        /// Gets or sets the size unit.
        /// </summary>
        public CacheSizeUnit SizeUnit { get; set; }

        /// <summary>
        /// Initializes a new instance of the size policy.
        /// </summary>
        protected SizePolicyBase(string name, long? maxSize, CacheSizeUnit sizeUnit) : base(name)
        {
            MaxSize = maxSize;
            SizeUnit = sizeUnit;
        }

        /// <summary>
        /// Calculates the size of a cache entry.
        /// </summary>
        public abstract long CalculateSize(ICacheEntry entry);

        /// <summary>
        /// Determines if adding an entry would exceed size limits.
        /// </summary>
        public virtual bool WouldExceedLimit(ICacheEntry entry, long currentSize)
        {
            if (!MaxSize.HasValue)
                return false;

            var entrySize = CalculateSize(entry);
            return currentSize + entrySize > MaxSize.Value;
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && (!MaxSize.HasValue || MaxSize.Value > 0);
        }
    }

    /// <summary>
    /// Base class for eviction policies.
    /// </summary>
    public abstract class EvictionPolicyBase : CachePolicyBase, IEvictionPolicy
    {
        /// <summary>
        /// Gets the policy type.
        /// </summary>
        public override CachePolicyType PolicyType => CachePolicyType.Eviction;

        /// <summary>
        /// Initializes a new instance of the eviction policy.
        /// </summary>
        protected EvictionPolicyBase(string name) : base(name)
        {
        }

        /// <summary>
        /// Selects entries for eviction.
        /// </summary>
        public abstract Task<IEnumerable<ICacheEntry>> SelectForEvictionAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates entry metadata after access.
        /// </summary>
        public virtual void OnEntryAccessed(ICacheEntry entry)
        {
            entry.RecordAccess();
        }

        /// <summary>
        /// Calculates the eviction score for an entry.
        /// </summary>
        public abstract double CalculateEvictionScore(ICacheEntry entry);
    }
}