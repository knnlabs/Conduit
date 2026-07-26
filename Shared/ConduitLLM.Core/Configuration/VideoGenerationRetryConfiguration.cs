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
        /// Jitter percentage applied to retry delays (0-100).
        /// Default: 20
        /// </summary>
        public int JitterPercentage { get; set; } = 20;

        /// <summary>
        /// Calculate the retry delay using exponential backoff with jitter.
        /// The order is deliberate: jitter first, then cap, then floor — so
        /// <see cref="MaxDelaySeconds"/> is a hard ceiling (jitter can never push past it)
        /// and the result is never less than 1 second.
        /// </summary>
        /// <param name="retryCount">Current retry attempt (0-based)</param>
        /// <returns>Delay in seconds before the next retry</returns>
        public int CalculateRetryDelay(int retryCount)
        {
            // Exponential backoff: BaseDelay * 2^retryCount
            var delay = BaseDelaySeconds * Math.Pow(2, retryCount);

            // Jitter (±JitterPercentage%)
            var jitterFraction = Math.Clamp(JitterPercentage, 0, 100) / 100.0;
            delay *= 1 + (Random.Shared.NextDouble() * 2 - 1) * jitterFraction;

            // Cap, then floor
            delay = Math.Min(delay, MaxDelaySeconds);
            return Math.Max(1, (int)delay);
        }
    }
}