namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Trend direction for image generation analytics.
    /// </summary>
    public enum ImageGenerationTrendDirection
    {
        Decreasing,
        Stable,
        Increasing
    }

    /// <summary>
    /// Trend direction for audio quality tracking.
    /// </summary>
    public enum AudioQualityTrendDirection
    {
        /// <summary>
        /// Quality is improving.
        /// </summary>
        Improving,

        /// <summary>
        /// Quality is stable.
        /// </summary>
        Stable,

        /// <summary>
        /// Quality is declining.
        /// </summary>
        Declining
    }
}