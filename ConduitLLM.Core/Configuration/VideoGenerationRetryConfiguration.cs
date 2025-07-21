using System;

namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Configuration settings for video generation retry logic.
    /// </summary>
    public class VideoGenerationRetryConfiguration
    {
        /// <summary>
        /// Maximum number of retry attempts for failed video generation tasks.
        /// Default: 3
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay in seconds for exponential backoff.
        /// The actual delay will be: BaseDelaySeconds * (2 ^ retryCount)
        /// Default: 30 seconds
        /// </summary>
        public int BaseDelaySeconds { get; set; } = 30;

        /// <summary>
        /// Maximum delay in seconds between retries.
        /// Default: 3600 seconds (1 hour)
        /// </summary>
        public int MaxDelaySeconds { get; set; } = 3600;

        /// <summary>
        /// Whether to enable automatic retries for failed tasks.
        /// Default: true
        /// </summary>
        public bool EnableRetries { get; set; } = true;

        /// <summary>
        /// Interval in seconds for checking tasks that need to be retried.
        /// Default: 30 seconds
        /// </summary>
        public int RetryCheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Calculate the retry delay using exponential backoff with jitter.
        /// </summary>
        /// <param name="retryCount">Current retry attempt (0-based)</param>
        /// <returns>Delay in seconds before the next retry</returns>
        public int CalculateRetryDelay(int retryCount)
        {
            // Exponential backoff: BaseDelay * 2^retryCount
            var delay = BaseDelaySeconds * Math.Pow(2, retryCount);
            
            // Add jitter (±20% randomization)
            var jitter = new Random().NextDouble() * 0.4 - 0.2; // -0.2 to +0.2
            delay = delay * (1 + jitter);
            
            // Cap at maximum delay
            return (int)Math.Min(delay, MaxDelaySeconds);
        }
    }
}