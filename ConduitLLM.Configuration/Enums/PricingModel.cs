namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Defines the pricing model strategy for calculating costs.
    /// </summary>
    public enum PricingModel
    {
        /// <summary>
        /// Standard token-based pricing (input/output tokens).
        /// Used by most text models (GPT, Claude, Llama, etc.).
        /// </summary>
        Standard = 0,

        /// <summary>
        /// Per-video flat rate pricing based on resolution and duration.
        /// Used by MiniMax for video generation (e.g., $0.10 for 512p 6s video).
        /// </summary>
        PerVideo = 1,

        /// <summary>
        /// Per-second video pricing with resolution multipliers.
        /// Used by Replicate for video generation (e.g., $0.09/second × resolution multiplier).
        /// </summary>
        PerSecondVideo = 2,

        /// <summary>
        /// Inference step-based pricing for image generation.
        /// Used by Fireworks AI (e.g., $0.00013 per step × 30 steps).
        /// </summary>
        InferenceSteps = 3,

        /// <summary>
        /// Context-length tiered token pricing.
        /// Used by MiniMax M1 (different rates for ≤200K vs >200K tokens).
        /// </summary>
        TieredTokens = 4,

        /// <summary>
        /// Per-image pricing with quality and resolution multipliers.
        /// Used by image generation models (DALL-E, Stable Diffusion, etc.).
        /// </summary>
        PerImage = 5,

        /// <summary>
        /// Audio pricing per minute of input/output.
        /// OBSOLETE - Audio functionality removed
        /// </summary>
        [Obsolete("Audio functionality has been removed from the system")]
        PerMinuteAudio = 6,

        /// <summary>
        /// Audio pricing per thousand characters.
        /// OBSOLETE - Audio functionality removed
        /// </summary>
        [Obsolete("Audio functionality has been removed from the system")]
        PerThousandCharacters = 7
    }
}