using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Models.Pricing;

/// <summary>
/// Pricing configuration for Tavily search API using credit-based pricing model.
/// </summary>
/// <remarks>
/// Tavily pricing structure (as of 2025):
/// - Basic search: 1 credit ($0.008 default)
/// - Advanced search: 2 credits ($0.016 default)
/// - Auto-parameters addon: 2 credits ($0.016 default)
/// - All content extraction (raw content, images, answer) included in search cost
///
/// Unlike Exa, Tavily does not charge separately for content extraction.
/// The search cost is all-inclusive based on search depth.
///
/// This configuration is stored as JSON in FunctionCost.PricingConfiguration.
/// </remarks>
public class TavilySearchPricingConfig
{
    /// <summary>
    /// Cost per credit in USD (default: $0.008).
    /// Based on Tavily's pay-as-you-go pricing.
    /// </summary>
    [JsonPropertyName("costPerCredit")]
    public decimal CostPerCredit { get; set; } = 0.008m;

    /// <summary>
    /// Credits consumed by basic search (default: 1).
    /// Basic search provides quick results with standard depth.
    /// </summary>
    [JsonPropertyName("basicSearchCredits")]
    public int BasicSearchCredits { get; set; } = 1;

    /// <summary>
    /// Credits consumed by advanced search (default: 2).
    /// Advanced search provides more comprehensive results with deeper analysis.
    /// Supports content chunks (1-3 chunks per source).
    /// </summary>
    [JsonPropertyName("advancedSearchCredits")]
    public int AdvancedSearchCredits { get; set; } = 2;

    /// <summary>
    /// Additional credits for auto-parameters feature (default: 2).
    /// When enabled, Tavily automatically configures optimal search parameters.
    /// Null means not supported or not charged separately.
    /// </summary>
    [JsonPropertyName("autoParametersCredits")]
    public int? AutoParametersCredits { get; set; } = 2;

    /// <summary>
    /// Whether to charge for answer generation separately.
    /// Default: false (included in search cost).
    /// This flag allows future-proofing if Tavily changes pricing model.
    /// </summary>
    [JsonPropertyName("chargeForAnswerGeneration")]
    public bool ChargeForAnswerGeneration { get; set; } = false;

    /// <summary>
    /// Cost for answer generation if charged separately (default: 0).
    /// Only applied if ChargeForAnswerGeneration is true.
    /// </summary>
    [JsonPropertyName("answerGenerationCost")]
    public decimal? AnswerGenerationCost { get; set; } = 0m;

    /// <summary>
    /// Whether to charge for image results separately.
    /// Default: false (included in search cost).
    /// </summary>
    [JsonPropertyName("chargeForImageResults")]
    public bool ChargeForImageResults { get; set; } = false;

    /// <summary>
    /// Cost per image result if charged separately (default: 0).
    /// Only applied if ChargeForImageResults is true.
    /// </summary>
    [JsonPropertyName("costPerImage")]
    public decimal? CostPerImage { get; set; } = 0m;
}
