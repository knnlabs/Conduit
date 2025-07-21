using System;

namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Configuration for image generation retry behavior
    /// </summary>
    public class ImageGenerationRetryConfiguration
    {
        /// <summary>
        /// Maximum number of retry attempts for failed image generation tasks
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay in seconds for exponential backoff retry logic
        /// </summary>
        public int BaseDelaySeconds { get; set; } = 30;

        /// <summary>
        /// Maximum delay in seconds between retry attempts
        /// </summary>
        public int MaxDelaySeconds { get; set; } = 3600; // 1 hour

        /// <summary>
        /// Whether retries are enabled for failed tasks
        /// </summary>
        public bool EnableRetries { get; set; } = true;

        /// <summary>
        /// How often to check for tasks ready to retry (in seconds)
        /// </summary>
        public int RetryCheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Jitter percentage to add randomness to retry delays (0-100)
        /// </summary>
        public int JitterPercentage { get; set; } = 20;

        /// <summary>
        /// Calculate the next retry delay using exponential backoff with jitter
        /// </summary>
        /// <param name="retryCount">Current retry attempt number</param>
        /// <returns>Delay in seconds until next retry</returns>
        public int CalculateRetryDelay(int retryCount)
        {
            // Exponential backoff: BaseDelay * 2^retryCount
            var delay = BaseDelaySeconds * Math.Pow(2, retryCount);
            
            // Cap at maximum delay
            delay = Math.Min(delay, MaxDelaySeconds);
            
            // Add jitter (±JitterPercentage%)
            var jitter = delay * (JitterPercentage / 100.0);
            var random = new Random();
            delay += (random.NextDouble() * 2 - 1) * jitter;
            
            return Math.Max(1, (int)delay); // Ensure at least 1 second delay
        }
    }
}