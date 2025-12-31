using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Models.Pricing;

/// <summary>
/// Generic tiered pricing configuration for functions with volume-based pricing.
/// </summary>
/// <remarks>
/// Supports flexible tier definitions based on usage thresholds.
/// Can be used for any function provider that charges different rates at different usage levels.
///
/// Example: Search API with volume discounts
/// - Tier 1: 1-100 results @ $0.01 per result
/// - Tier 2: 101-1000 results @ $0.008 per result
/// - Tier 3: 1001+ results @ $0.005 per result
/// </remarks>
public class TieredPricingConfig
{
    /// <summary>
    /// List of pricing tiers, ordered from lowest to highest threshold.
    /// </summary>
    /// <remarks>
    /// Tiers must be ordered by threshold ascending.
    /// The last tier typically has MaxThreshold = null, meaning it applies to all usage beyond the previous tier.
    /// </remarks>
    [JsonPropertyName("tiers")]
    public required List<PricingTier> Tiers { get; set; }

    /// <summary>
    /// The unit being billed (e.g., "result", "token", "document", "query").
    /// </summary>
    /// <remarks>
    /// Used for display and documentation purposes.
    /// Example values: "result", "token", "document", "query", "chunk", "vector"
    /// </remarks>
    [JsonPropertyName("billingUnit")]
    public string BillingUnit { get; set; } = "result";
}

/// <summary>
/// Represents a single pricing tier in a volume-based pricing model.
/// </summary>
public class PricingTier
{
    /// <summary>
    /// Minimum units for this tier to apply (inclusive).
    /// </summary>
    /// <remarks>
    /// If null, this tier starts from 1 (or from the end of the previous tier).
    /// </remarks>
    [JsonPropertyName("minThreshold")]
    public int? MinThreshold { get; set; }

    /// <summary>
    /// Maximum units for this tier to apply (inclusive).
    /// </summary>
    /// <remarks>
    /// If null, this tier applies to all usage beyond MinThreshold with no upper limit.
    /// </remarks>
    [JsonPropertyName("maxThreshold")]
    public int? MaxThreshold { get; set; }

    /// <summary>
    /// Cost per unit within this tier.
    /// </summary>
    [JsonPropertyName("costPerUnit")]
    public decimal CostPerUnit { get; set; }
}
