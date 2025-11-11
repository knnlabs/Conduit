using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Policies
{
    /// <summary>
    /// Fixed TTL policy - entries expire after a fixed duration from creation.
    /// </summary>
    public class FixedTtlPolicy : TtlPolicyBase
    {
        /// <summary>
        /// Gets or sets the fixed TTL duration.
        /// </summary>
        public TimeSpan Ttl { get; set; }

        /// <summary>
        /// Initializes a new instance of the fixed TTL policy.
        /// </summary>
        public FixedTtlPolicy(string name, TimeSpan ttl) : base(name)
        {
            Ttl = ttl;
        }

        /// <summary>
        /// Calculates expiration based on creation time plus fixed TTL.
        /// </summary>
        public override DateTime? CalculateExpiration(ICacheEntry entry, CachePolicyContext context)
        {
            if (Ttl == TimeSpan.Zero || Ttl == TimeSpan.MaxValue)
                return null;

            return entry.CreatedAt.Add(Ttl);
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && Ttl >= TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Sliding TTL policy - entries expire after a duration of inactivity.
    /// </summary>
    public class SlidingTtlPolicy : TtlPolicyBase
    {
        /// <summary>
        /// Gets or sets the sliding window duration.
        /// </summary>
        public TimeSpan SlidingWindow { get; set; }

        /// <summary>
        /// Gets or sets the maximum total lifetime (optional).
        /// </summary>
        public TimeSpan? MaxLifetime { get; set; }

        /// <summary>
        /// Initializes a new instance of the sliding TTL policy.
        /// </summary>
        public SlidingTtlPolicy(string name, TimeSpan slidingWindow, TimeSpan? maxLifetime = null) : base(name)
        {
            SlidingWindow = slidingWindow;
            MaxLifetime = maxLifetime;
        }

        /// <summary>
        /// Calculates expiration based on last access time plus sliding window.
        /// </summary>
        public override DateTime? CalculateExpiration(ICacheEntry entry, CachePolicyContext context)
        {
            if (SlidingWindow == TimeSpan.Zero || SlidingWindow == TimeSpan.MaxValue)
                return null;

            var slidingExpiration = entry.LastAccessedAt.Add(SlidingWindow);

            // Apply maximum lifetime if configured
            if (MaxLifetime.HasValue)
            {
                var maxExpiration = entry.CreatedAt.Add(MaxLifetime.Value);
                return slidingExpiration < maxExpiration ? slidingExpiration : maxExpiration;
            }

            return slidingExpiration;
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   SlidingWindow >= TimeSpan.Zero &&
                   (!MaxLifetime.HasValue || MaxLifetime.Value >= TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Adaptive TTL policy - adjusts TTL based on access patterns.
    /// </summary>
    public class AdaptiveTtlPolicy : TtlPolicyBase
    {
        /// <summary>
        /// Gets or sets the minimum TTL.
        /// </summary>
        public TimeSpan MinTtl { get; set; }

        /// <summary>
        /// Gets or sets the maximum TTL.
        /// </summary>
        public TimeSpan MaxTtl { get; set; }

        /// <summary>
        /// Gets or sets the access count threshold for extending TTL.
        /// </summary>
        public long AccessThreshold { get; set; }

        /// <summary>
        /// Gets or sets the TTL extension factor per threshold reached.
        /// </summary>
        public double ExtensionFactor { get; set; }

        /// <summary>
        /// Initializes a new instance of the adaptive TTL policy.
        /// </summary>
        public AdaptiveTtlPolicy(string name, TimeSpan minTtl, TimeSpan maxTtl, long accessThreshold = 10, double extensionFactor = 1.5) 
            : base(name)
        {
            MinTtl = minTtl;
            MaxTtl = maxTtl;
            AccessThreshold = accessThreshold;
            ExtensionFactor = extensionFactor;
        }

        /// <summary>
        /// Calculates expiration based on access patterns.
        /// </summary>
        public override DateTime? CalculateExpiration(ICacheEntry entry, CachePolicyContext context)
        {
            if (MinTtl == TimeSpan.Zero || MinTtl == TimeSpan.MaxValue)
                return null;

            // Start with minimum TTL
            var ttl = MinTtl;

            // Extend TTL based on access count
            if (AccessThreshold > 0 && entry.AccessCount > AccessThreshold)
            {
                var multiplier = Math.Floor((double)entry.AccessCount / AccessThreshold);
                var extendedTtl = TimeSpan.FromMilliseconds(MinTtl.TotalMilliseconds * Math.Pow(ExtensionFactor, multiplier));
                
                // Cap at maximum TTL
                ttl = extendedTtl > MaxTtl ? MaxTtl : extendedTtl;
            }

            return DateTime.UtcNow.Add(ttl);
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   MinTtl >= TimeSpan.Zero &&
                   MaxTtl >= MinTtl &&
                   AccessThreshold >= 0 &&
                   ExtensionFactor >= 1.0;
        }
    }

    /// <summary>
    /// Time-based TTL policy - different TTLs based on time of day.
    /// </summary>
    public class TimeBasedTtlPolicy : TtlPolicyBase
    {
        /// <summary>
        /// Time-based TTL rules.
        /// </summary>
        public List<TimeBasedRule> Rules { get; set; } = new();

        /// <summary>
        /// Default TTL when no rules match.
        /// </summary>
        public TimeSpan DefaultTtl { get; set; }

        /// <summary>
        /// Initializes a new instance of the time-based TTL policy.
        /// </summary>
        public TimeBasedTtlPolicy(string name, TimeSpan defaultTtl) : base(name)
        {
            DefaultTtl = defaultTtl;
        }

        /// <summary>
        /// Calculates expiration based on current time rules.
        /// </summary>
        public override DateTime? CalculateExpiration(ICacheEntry entry, CachePolicyContext context)
        {
            var now = context.RequestTime;
            var ttl = DefaultTtl;

            // Find matching rule
            foreach (var rule in Rules.OrderByDescending(r => r.Priority))
            {
                if (rule.Matches(now))
                {
                    ttl = rule.Ttl;
                    break;
                }
            }

            if (ttl == TimeSpan.Zero || ttl == TimeSpan.MaxValue)
                return null;

            return now.Add(ttl);
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   DefaultTtl >= TimeSpan.Zero &&
                   Rules.All(r => r.Validate());
        }

        /// <summary>
        /// Time-based rule for TTL.
        /// </summary>
        public class TimeBasedRule
        {
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public DayOfWeek[] DaysOfWeek { get; set; } = Array.Empty<DayOfWeek>();
            public TimeSpan Ttl { get; set; }
            public int Priority { get; set; }

            public bool Matches(DateTime time)
            {
                // Check day of week if specified
                if (DaysOfWeek.Length > 0 && !DaysOfWeek.Contains(time.DayOfWeek))
                    return false;

                // Check time of day
                var timeOfDay = time.TimeOfDay;
                
                if (StartTime <= EndTime)
                {
                    // Normal case: start before end (e.g., 9:00 - 17:00)
                    return timeOfDay >= StartTime && timeOfDay <= EndTime;
                }
                else
                {
                    // Overnight case: start after end (e.g., 22:00 - 06:00)
                    return timeOfDay >= StartTime || timeOfDay <= EndTime;
                }
            }

            public bool Validate()
            {
                return Ttl >= TimeSpan.Zero && Priority >= 0;
            }
        }
    }
}