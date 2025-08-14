using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Policies
{
    /// <summary>
    /// LRU (Least Recently Used) eviction policy.
    /// </summary>
    public class LruEvictionPolicy : EvictionPolicyBase
    {
        /// <summary>
        /// Initializes a new instance of the LRU eviction policy.
        /// </summary>
        public LruEvictionPolicy(string name) : base(name)
        {
        }

        /// <summary>
        /// Selects least recently used entries for eviction.
        /// </summary>
        public override Task<IEnumerable<ICacheEntry>> SelectForEvictionAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            var entriesList = entries.ToList();
            var selectedEntries = new List<ICacheEntry>();
            long freedSpace = 0;

            // Sort by last accessed time (oldest first)
            var sortedEntries = entriesList
                .OrderBy(e => e.LastAccessedAt)
                .ToList();

            foreach (var entry in sortedEntries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                selectedEntries.Add(entry);
                freedSpace += entry.SizeInBytes ?? 1;

                if (freedSpace >= spaceNeeded)
                    break;
            }

            return Task.FromResult<IEnumerable<ICacheEntry>>(selectedEntries);
        }

        /// <summary>
        /// Calculates eviction score based on last access time.
        /// </summary>
        public override double CalculateEvictionScore(ICacheEntry entry)
        {
            // Lower score = more likely to evict
            // Use ticks as score (older entries have lower scores)
            return entry.LastAccessedAt.Ticks;
        }
    }

    /// <summary>
    /// LFU (Least Frequently Used) eviction policy.
    /// </summary>
    public class LfuEvictionPolicy : EvictionPolicyBase
    {
        /// <summary>
        /// Gets or sets the time window for frequency calculation.
        /// </summary>
        public TimeSpan? FrequencyWindow { get; set; }

        /// <summary>
        /// Initializes a new instance of the LFU eviction policy.
        /// </summary>
        public LfuEvictionPolicy(string name, TimeSpan? frequencyWindow = null) : base(name)
        {
            FrequencyWindow = frequencyWindow;
        }

        /// <summary>
        /// Selects least frequently used entries for eviction.
        /// </summary>
        public override Task<IEnumerable<ICacheEntry>> SelectForEvictionAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            var entriesList = entries.ToList();
            var selectedEntries = new List<ICacheEntry>();
            long freedSpace = 0;

            // Calculate frequency scores and sort
            var scoredEntries = entriesList
                .Select(e => new { Entry = e, Score = CalculateFrequencyScore(e) })
                .OrderBy(x => x.Score)
                .Select(x => x.Entry)
                .ToList();

            foreach (var entry in scoredEntries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                selectedEntries.Add(entry);
                freedSpace += entry.SizeInBytes ?? 1;

                if (freedSpace >= spaceNeeded)
                    break;
            }

            return Task.FromResult<IEnumerable<ICacheEntry>>(selectedEntries);
        }

        /// <summary>
        /// Calculates eviction score based on access frequency.
        /// </summary>
        public override double CalculateEvictionScore(ICacheEntry entry)
        {
            return CalculateFrequencyScore(entry);
        }

        private double CalculateFrequencyScore(ICacheEntry entry)
        {
            if (FrequencyWindow.HasValue)
            {
                // Calculate frequency within the time window
                var age = DateTime.UtcNow - entry.CreatedAt;
                var effectiveAge = age > FrequencyWindow.Value ? FrequencyWindow.Value : age;
                
                if (effectiveAge.TotalSeconds <= 0)
                    return double.MaxValue;

                // Frequency = access count / time window
                return entry.AccessCount / effectiveAge.TotalSeconds;
            }
            else
            {
                // Simple frequency: just use access count
                return entry.AccessCount;
            }
        }
    }

    /// <summary>
    /// Priority-based eviction policy.
    /// </summary>
    public class PriorityEvictionPolicy : EvictionPolicyBase
    {
        /// <summary>
        /// Gets or sets whether to consider age in addition to priority.
        /// </summary>
        public bool ConsiderAge { get; set; } = true;

        /// <summary>
        /// Gets or sets the age weight factor.
        /// </summary>
        public double AgeWeight { get; set; } = 0.3;

        /// <summary>
        /// Initializes a new instance of the priority eviction policy.
        /// </summary>
        public PriorityEvictionPolicy(string name) : base(name)
        {
        }

        /// <summary>
        /// Selects entries for eviction based on priority.
        /// </summary>
        public override Task<IEnumerable<ICacheEntry>> SelectForEvictionAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            var entriesList = entries.ToList();
            var selectedEntries = new List<ICacheEntry>();
            long freedSpace = 0;

            // Sort by eviction score (lower priority entries first)
            var sortedEntries = entriesList
                .OrderBy(e => CalculateEvictionScore(e))
                .ToList();

            foreach (var entry in sortedEntries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                selectedEntries.Add(entry);
                freedSpace += entry.SizeInBytes ?? 1;

                if (freedSpace >= spaceNeeded)
                    break;
            }

            return Task.FromResult<IEnumerable<ICacheEntry>>(selectedEntries);
        }

        /// <summary>
        /// Calculates eviction score based on priority and optionally age.
        /// </summary>
        public override double CalculateEvictionScore(ICacheEntry entry)
        {
            double score = entry.Priority;

            if (ConsiderAge)
            {
                // Factor in age: older entries get lower scores
                var age = (DateTime.UtcNow - entry.CreatedAt).TotalSeconds;
                var ageScore = 1.0 / (1.0 + age); // Decay function: newer = higher score
                
                // Combine priority and age
                score = (entry.Priority * (1 - AgeWeight)) + (ageScore * 100 * AgeWeight);
            }

            return score;
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   AgeWeight >= 0 && AgeWeight <= 1;
        }
    }

    /// <summary>
    /// Composite eviction policy that combines multiple policies.
    /// </summary>
    public class CompositeEvictionPolicy : EvictionPolicyBase
    {
        /// <summary>
        /// Gets the sub-policies with their weights.
        /// </summary>
        public List<(IEvictionPolicy Policy, double Weight)> Policies { get; set; } = new();

        /// <summary>
        /// Initializes a new instance of the composite eviction policy.
        /// </summary>
        public CompositeEvictionPolicy(string name) : base(name)
        {
        }

        /// <summary>
        /// Selects entries for eviction using weighted scores from all policies.
        /// </summary>
        public override Task<IEnumerable<ICacheEntry>> SelectForEvictionAsync(
            IEnumerable<ICacheEntry> entries,
            long spaceNeeded,
            CachePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            if (Policies.Count() == 0)
                return Task.FromResult(Enumerable.Empty<ICacheEntry>());

            var entriesList = entries.ToList();
            var selectedEntries = new List<ICacheEntry>();
            long freedSpace = 0;

            // Calculate composite scores
            var scoredEntries = new List<(ICacheEntry Entry, double Score)>();
            
            foreach (var entry in entriesList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var compositeScore = CalculateEvictionScore(entry);
                scoredEntries.Add((entry, compositeScore));
            }

            // Sort by composite score (lower scores evicted first)
            var sortedEntries = scoredEntries
                .OrderBy(x => x.Score)
                .Select(x => x.Entry)
                .ToList();

            foreach (var entry in sortedEntries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                selectedEntries.Add(entry);
                freedSpace += entry.SizeInBytes ?? 1;

                if (freedSpace >= spaceNeeded)
                    break;
            }

            return Task.FromResult((IEnumerable<ICacheEntry>)selectedEntries);
        }

        /// <summary>
        /// Calculates composite eviction score from all sub-policies.
        /// </summary>
        public override double CalculateEvictionScore(ICacheEntry entry)
        {
            if (Policies.Count() == 0)
                return 0;

            double totalWeight = Policies.Sum(p => p.Weight);
            if (totalWeight <= 0)
                return 0;

            double compositeScore = 0;
            
            foreach (var (policy, weight) in Policies)
            {
                if (policy.IsEnabled)
                {
                    var score = policy.CalculateEvictionScore(entry);
                    compositeScore += score * (weight / totalWeight);
                }
            }

            return compositeScore;
        }

        /// <summary>
        /// Updates entry metadata using all sub-policies.
        /// </summary>
        public override void OnEntryAccessed(ICacheEntry entry)
        {
            base.OnEntryAccessed(entry);
            
            foreach (var (policy, _) in Policies)
            {
                if (policy.IsEnabled)
                {
                    policy.OnEntryAccessed(entry);
                }
            }
        }

        /// <summary>
        /// Validates the policy configuration.
        /// </summary>
        public override bool Validate()
        {
            return base.Validate() && 
                   Policies.All(p => p.Policy.Validate() && p.Weight >= 0) &&
                   Policies.Any(p => p.Weight > 0);
        }
    }
}