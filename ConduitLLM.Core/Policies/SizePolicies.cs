using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Policies
{
    /// <summary>
    /// Item count size policy - limits cache by number of items.
    /// </summary>
    public class ItemCountSizePolicy : SizePolicyBase
    {
        /// <summary>
        /// Initializes a new instance of the item count size policy.
        /// </summary>
        public ItemCountSizePolicy(string name, long maxItems) 
            : base(name, maxItems, CacheSizeUnit.Items)
        {
        }

        /// <summary>
        /// Calculates size as 1 item.
        /// </summary>
        public override long CalculateSize(ICacheEntry entry)
        {
            return 1;
        }
    }

    /// <summary>
    /// Memory size policy - limits cache by memory usage.
    /// </summary>
    public class MemorySizePolicy : SizePolicyBase
    {
        /// <summary>
        /// Gets or sets whether to estimate size if not provided.
        /// </summary>
        public bool EstimateSizeIfMissing { get; set; } = true;

        /// <summary>
        /// Gets or sets the default size estimate for unknown objects.
        /// </summary>
        public long DefaultSizeEstimate { get; set; } = 1024; // 1KB default

        /// <summary>
        /// Initializes a new instance of the memory size policy.
        /// </summary>
        public MemorySizePolicy(string name, long maxSizeBytes) 
            : base(name, maxSizeBytes, CacheSizeUnit.Bytes)
        {
        }

        /// <summary>
        /// Calculates the size of a cache entry in bytes.
        /// </summary>
        public override long CalculateSize(ICacheEntry entry)
        {
            // Use provided size if available
            if (entry.SizeInBytes.HasValue)
                return entry.SizeInBytes.Value;

            // Estimate size if enabled
            if (EstimateSizeIfMissing && entry.Value != null)
                return EstimateObjectSize(entry.Value);

            return DefaultSizeEstimate;
        }

        /// <summary>
        /// Estimates the size of an object.
        /// </summary>
        private long EstimateObjectSize(object obj)
        {
            try
            {
                // Simple estimation using JSON serialization
                var json = JsonSerializer.Serialize(obj);
                return Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                // Fallback for non-serializable objects
                return EstimatePrimitiveSize(obj);
            }
        }

        /// <summary>
        /// Estimates size for primitive types.
        /// </summary>
        private long EstimatePrimitiveSize(object obj)
        {
            return obj switch
            {
                string str => Encoding.UTF8.GetByteCount(str),
                byte[] bytes => bytes.Length,
                bool => sizeof(bool),
                char => sizeof(char),
                sbyte => sizeof(sbyte),
                byte => sizeof(byte),
                short => sizeof(short),
                ushort => sizeof(ushort),
                int => sizeof(int),
                uint => sizeof(uint),
                long => sizeof(long),
                ulong => sizeof(ulong),
                float => sizeof(float),
                double => sizeof(double),
                decimal => sizeof(decimal),
                DateTime => 8,
                Guid => 16,
                _ => DefaultSizeEstimate
            };
        }
    }

    /// <summary>
    /// Dynamic size policy - adjusts size limits based on system resources.
    /// </summary>
    public class DynamicSizePolicy : SizePolicyBase
    {
        /// <summary>
        /// Gets or sets the target memory usage percentage.
        /// </summary>
        public double TargetMemoryPercentage { get; set; }

        /// <summary>
        /// Gets or sets the minimum size limit.
        /// </summary>
        public long MinSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum size limit.
        /// </summary>
        public long MaxSizeLimit { get; set; }

        /// <summary>
        /// Gets or sets how often to recalculate limits.
        /// </summary>
        public TimeSpan RecalculationInterval { get; set; }

        private DateTime _lastRecalculation = DateTime.MinValue;
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the dynamic size policy.
        /// </summary>
        public DynamicSizePolicy(string name, double targetMemoryPercentage, long minSize, long maxSize) 
            : base(name, null, CacheSizeUnit.Bytes)
        {
            TargetMemoryPercentage = targetMemoryPercentage;
            MinSize = minSize;
            MaxSizeLimit = maxSize;
            RecalculationInterval = TimeSpan.FromMinutes(1);
            RecalculateMaxSize();
        }

        /// <summary>
        /// Calculates the size of a cache entry.
        /// </summary>
        public override long CalculateSize(ICacheEntry entry)
        {
            // Recalculate max size if needed
            RecalculateIfNeeded();

            // Use memory size calculation
            return entry.SizeInBytes ?? 1024; // Default 1KB
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   TargetMemoryPercentage > 0 && TargetMemoryPercentage <= 100 &&
                   MinSize > 0 &&
                   MaxSizeLimit >= MinSize;
        }

        private void RecalculateIfNeeded()
        {
            if (DateTime.UtcNow - _lastRecalculation > RecalculationInterval)
            {
                lock (_lock)
                {
                    if (DateTime.UtcNow - _lastRecalculation > RecalculationInterval)
                    {
                        RecalculateMaxSize();
                    }
                }
            }
        }

        private void RecalculateMaxSize()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var targetSize = (long)(totalMemory * (TargetMemoryPercentage / 100.0));
            
            // Apply min/max bounds
            targetSize = Math.Max(MinSize, Math.Min(MaxSizeLimit, targetSize));
            
            MaxSize = targetSize;
            _lastRecalculation = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tiered size policy - different size limits based on priority.
    /// </summary>
    public class TieredSizePolicy : SizePolicyBase
    {
        /// <summary>
        /// Size tiers based on priority.
        /// </summary>
        public List<SizeTier> Tiers { get; set; } = new();

        /// <summary>
        /// Initializes a new instance of the tiered size policy.
        /// </summary>
        public TieredSizePolicy(string name) 
            : base(name, null, CacheSizeUnit.Items)
        {
        }

        /// <summary>
        /// Calculates size considering the entry's tier.
        /// </summary>
        public override long CalculateSize(ICacheEntry entry)
        {
            return 1; // Each entry counts as 1 item
        }

        /// <summary>
        /// Determines if adding an entry would exceed tier limits.
        /// </summary>
        public override bool WouldExceedLimit(ICacheEntry entry, long currentSize)
        {
            var tier = GetTierForEntry(entry);
            if (tier == null)
                return false;

            // Count entries in this tier
            // In a real implementation, we'd track this more efficiently
            return currentSize >= tier.MaxItems;
        }

        private SizeTier? GetTierForEntry(ICacheEntry entry)
        {
            return Tiers
                .Where(t => entry.Priority >= t.MinPriority && entry.Priority <= t.MaxPriority)
                .OrderByDescending(t => t.MinPriority)
                .FirstOrDefault();
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   Tiers.All(t => t.Validate()) &&
                   !HasOverlappingTiers();
        }

        private bool HasOverlappingTiers()
        {
            for (int i = 0; i < Tiers.Count - 1; i++)
            {
                for (int j = i + 1; j < Tiers.Count; j++)
                {
                    if (Tiers[i].Overlaps(Tiers[j]))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Represents a size tier.
        /// </summary>
        public class SizeTier
        {
            public int MinPriority { get; set; }
            public int MaxPriority { get; set; }
            public long MaxItems { get; set; }
            public string Name { get; set; } = string.Empty;

            public bool Overlaps(SizeTier other)
            {
                return MinPriority <= other.MaxPriority && MaxPriority >= other.MinPriority;
            }

            public bool Validate()
            {
                return MinPriority >= 0 && 
                       MaxPriority >= MinPriority && 
                       MaxPriority <= 100 &&
                       MaxItems > 0;
            }
        }
    }
}