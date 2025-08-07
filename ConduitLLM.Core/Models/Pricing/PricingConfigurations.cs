using System.Collections.Generic;

namespace ConduitLLM.Core.Models.Pricing
{
    /// <summary>
    /// Configuration for per-video flat rate pricing (MiniMax).
    /// </summary>
    public class PerVideoPricingConfig
    {
        /// <summary>
        /// Flat rates for specific resolution and duration combinations.
        /// Key format: "{resolution}_{duration}" (e.g., "512p_6", "1080p_10").
        /// Value is the flat cost in USD.
        /// </summary>
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }

    /// <summary>
    /// Configuration for per-second video pricing with resolution multipliers (Replicate).
    /// </summary>
    public class PerSecondVideoPricingConfig
    {
        /// <summary>
        /// Base cost per second of video in USD.
        /// </summary>
        public decimal BaseRate { get; set; }

        /// <summary>
        /// Resolution-based multipliers.
        /// Key is resolution (e.g., "720p", "1080p"), value is multiplier.
        /// </summary>
        public Dictionary<string, decimal> ResolutionMultipliers { get; set; } = new();
    }

    /// <summary>
    /// Configuration for inference step-based pricing (Fireworks).
    /// </summary>
    public class InferenceStepsPricingConfig
    {
        /// <summary>
        /// Cost per inference step in USD.
        /// </summary>
        public decimal CostPerStep { get; set; }

        /// <summary>
        /// Default number of steps if not specified in request.
        /// </summary>
        public int DefaultSteps { get; set; }

        /// <summary>
        /// Model-specific step counts.
        /// Key is model variant, value is default steps for that variant.
        /// </summary>
        public Dictionary<string, int>? ModelSteps { get; set; }
    }

    /// <summary>
    /// Configuration for context-length tiered token pricing (MiniMax M1).
    /// </summary>
    public class TieredTokensPricingConfig
    {
        /// <summary>
        /// Pricing tiers based on context length.
        /// </summary>
        public List<TokenPricingTier> Tiers { get; set; } = new();
    }

    /// <summary>
    /// Represents a single pricing tier for token-based models.
    /// </summary>
    public class TokenPricingTier
    {
        /// <summary>
        /// Maximum context length for this tier (null for unlimited).
        /// </summary>
        public int? MaxContext { get; set; }

        /// <summary>
        /// Input cost per million tokens for this tier.
        /// </summary>
        public decimal InputCost { get; set; }

        /// <summary>
        /// Output cost per million tokens for this tier.
        /// </summary>
        public decimal OutputCost { get; set; }
    }

    /// <summary>
    /// Configuration for per-image pricing with multipliers.
    /// </summary>
    public class PerImagePricingConfig
    {
        /// <summary>
        /// Base cost per image in USD.
        /// </summary>
        public decimal BaseRate { get; set; }

        /// <summary>
        /// Quality-based multipliers.
        /// Key is quality level (e.g., "standard", "hd"), value is multiplier.
        /// </summary>
        public Dictionary<string, decimal>? QualityMultipliers { get; set; }

        /// <summary>
        /// Resolution-based multipliers.
        /// Key is resolution (e.g., "1024x1024", "1792x1024"), value is multiplier.
        /// </summary>
        public Dictionary<string, decimal>? ResolutionMultipliers { get; set; }
    }
}