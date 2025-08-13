using System;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for the batch spending update service
    /// </summary>
    public class BatchSpendingOptions
    {
        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string SectionName = "BatchSpending";

        /// <summary>
        /// Interval in seconds between batch flushes
        /// </summary>
        [Range(1, 86400)] // 1 second to 24 hours
        public int FlushIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Minimum allowed flush interval in seconds (safety floor)
        /// </summary>
        [Range(1, 3600)] // 1 second to 1 hour
        public int MinimumIntervalSeconds { get; set; } = 1;

        /// <summary>
        /// Maximum allowed flush interval in seconds (safety ceiling)
        /// Default: 6 hours (21600 seconds) - well below Redis TTL
        /// </summary>
        [Range(60, 86400)] // 1 minute to 24 hours
        public int MaximumIntervalSeconds { get; set; } = 21600; // 6 hours

        /// <summary>
        /// Redis TTL in hours for pending spend keys
        /// </summary>
        [Range(1, 168)] // 1 hour to 1 week
        public int RedisTtlHours { get; set; } = 24;

        /// <summary>
        /// Gets the validated flush interval as TimeSpan
        /// </summary>
        public TimeSpan GetValidatedFlushInterval()
        {
            // Apply minimum and maximum bounds
            var clampedInterval = Math.Max(MinimumIntervalSeconds, 
                Math.Min(FlushIntervalSeconds, MaximumIntervalSeconds));

            // Additional safety check: ensure interval is well below Redis TTL
            var maxSafeInterval = (int)TimeSpan.FromHours(RedisTtlHours - 1).TotalSeconds; // 1 hour buffer
            clampedInterval = Math.Min(clampedInterval, maxSafeInterval);

            return TimeSpan.FromSeconds(clampedInterval);
        }

        /// <summary>
        /// Gets the Redis TTL as TimeSpan
        /// </summary>
        public TimeSpan GetRedisTtl()
        {
            return TimeSpan.FromHours(RedisTtlHours);
        }

        /// <summary>
        /// Validates the configuration and returns validation errors if any
        /// </summary>
        public ValidationResult? Validate()
        {
            if (FlushIntervalSeconds < MinimumIntervalSeconds)
            {
                return new ValidationResult(
                    $"FlushIntervalSeconds ({FlushIntervalSeconds}) cannot be less than MinimumIntervalSeconds ({MinimumIntervalSeconds})",
                    new[] { nameof(FlushIntervalSeconds) });
            }

            if (FlushIntervalSeconds > MaximumIntervalSeconds)
            {
                return new ValidationResult(
                    $"FlushIntervalSeconds ({FlushIntervalSeconds}) cannot be greater than MaximumIntervalSeconds ({MaximumIntervalSeconds})",
                    new[] { nameof(FlushIntervalSeconds) });
            }

            var maxSafeInterval = TimeSpan.FromHours(RedisTtlHours - 1).TotalSeconds;
            if (FlushIntervalSeconds >= maxSafeInterval)
            {
                return new ValidationResult(
                    $"FlushIntervalSeconds ({FlushIntervalSeconds}) must be less than Redis TTL minus 1 hour ({maxSafeInterval} seconds) to prevent transaction loss",
                    new[] { nameof(FlushIntervalSeconds), nameof(RedisTtlHours) });
            }

            return null;
        }
    }
}